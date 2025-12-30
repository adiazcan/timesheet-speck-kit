import React, { useState, useEffect } from 'react';
import { AlertTriangle, Trash2, XCircle, CheckCircle, Clock } from 'lucide-react';

interface DeletionRequest {
  id: string;
  employeeId: string;
  employeeEmail: string;
  requestedAt: string;
  scheduledDeletionDate: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Cancelled' | 'Failed';
  conversationsDeleted?: number;
  completedAt?: string;
  cancellationReason?: string;
}

/**
 * Component for GDPR data deletion requests
 * FR-014b: Employee self-service mechanism to request conversation data deletion
 * FR-014c: Process deletion requests within 30 days and confirm completion
 */
export const DataDeletionRequest: React.FC = () => {
  const [pendingRequest, setPendingRequest] = useState<DeletionRequest | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showConfirmation, setShowConfirmation] = useState(false);

  useEffect(() => {
    fetchPendingRequest();
  }, []);

  const fetchPendingRequest = async () => {
    try {
      const response = await fetch('/api/conversation/deletion-request', {
        headers: {
          Authorization: `Bearer ${localStorage.getItem('authToken')}`,
        },
      });

      if (response.status === 204) {
        // No pending request
        setPendingRequest(null);
        return;
      }

      if (response.ok) {
        const data = await response.json();
        setPendingRequest(data);
      }
    } catch (err) {
      console.error('Failed to fetch deletion request:', err);
    }
  };

  const submitDeletionRequest = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch('/api/conversation/deletion-request', {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${localStorage.getItem('authToken')}`,
          'Content-Type': 'application/json',
        },
      });

      if (response.ok) {
        const data = await response.json();
        setPendingRequest(data);
        setShowConfirmation(false);
        // Success message handled by UI display
      } else {
        const errorData = await response.json();
        setError(errorData.detail || 'Failed to submit deletion request');
      }
    } catch (err) {
      setError('An error occurred while submitting your request');
      console.error('Deletion request error:', err);
    } finally {
      setLoading(false);
    }
  };

  const cancelDeletionRequest = async () => {
    if (!pendingRequest) return;

    setLoading(true);
    setError(null);

    try {
      const response = await fetch(`/api/conversation/deletion-request/${pendingRequest.id}`, {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${localStorage.getItem('authToken')}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ reason: 'Cancelled by user' }),
      });

      if (response.ok) {
        setPendingRequest(null);
      } else {
        const errorData = await response.json();
        setError(errorData.detail || 'Failed to cancel deletion request');
      }
    } catch (err) {
      setError('An error occurred while cancelling your request');
      console.error('Cancellation error:', err);
    } finally {
      setLoading(false);
    }
  };

  const calculateDaysRemaining = (scheduledDate: string) => {
    const scheduled = new Date(scheduledDate);
    const now = new Date();
    const diffTime = scheduled.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return Math.max(0, diffDays);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  // Render status badge
  const renderStatusBadge = (status: string) => {
    const statusConfig = {
      Pending: { icon: Clock, color: 'text-yellow-600 bg-yellow-50', label: 'Pending' },
      Processing: { icon: Clock, color: 'text-blue-600 bg-blue-50', label: 'Processing' },
      Completed: { icon: CheckCircle, color: 'text-green-600 bg-green-50', label: 'Completed' },
      Cancelled: { icon: XCircle, color: 'text-gray-600 bg-gray-50', label: 'Cancelled' },
      Failed: { icon: AlertTriangle, color: 'text-red-600 bg-red-50', label: 'Failed' },
    };

    const config = statusConfig[status as keyof typeof statusConfig];
    const Icon = config.icon;

    return (
      <span
        className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium ${config.color}`}
      >
        <Icon className="w-4 h-4 mr-1" />
        {config.label}
      </span>
    );
  };

  return (
    <div className="max-w-3xl mx-auto p-6">
      <div className="bg-white rounded-lg shadow-md p-6">
        <h2 className="text-2xl font-bold text-gray-900 mb-4">Data Deletion Request</h2>

        {error && (
          <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded-md">
            <div className="flex items-center">
              <AlertTriangle className="w-5 h-5 text-red-600 mr-2" />
              <p className="text-sm text-red-800">{error}</p>
            </div>
          </div>
        )}

        {pendingRequest ? (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-gray-900">Active Deletion Request</h3>
              {renderStatusBadge(pendingRequest.status)}
            </div>

            <div className="bg-gray-50 rounded-md p-4 space-y-3">
              <div>
                <span className="text-sm font-medium text-gray-500">Request ID:</span>
                <p className="text-sm text-gray-900 font-mono">{pendingRequest.id}</p>
              </div>

              <div>
                <span className="text-sm font-medium text-gray-500">Requested:</span>
                <p className="text-sm text-gray-900">{formatDate(pendingRequest.requestedAt)}</p>
              </div>

              <div>
                <span className="text-sm font-medium text-gray-500">Scheduled Deletion:</span>
                <p className="text-sm text-gray-900">
                  {formatDate(pendingRequest.scheduledDeletionDate)}
                </p>
              </div>

              {pendingRequest.status === 'Pending' && (
                <div className="pt-2 border-t border-gray-200">
                  <div className="flex items-center">
                    <Clock className="w-5 h-5 text-blue-600 mr-2" />
                    <span className="text-sm font-medium text-gray-900">
                      {calculateDaysRemaining(pendingRequest.scheduledDeletionDate)} days remaining
                    </span>
                  </div>
                  <p className="text-xs text-gray-600 mt-1">
                    You can cancel this request at any time before the scheduled deletion date.
                  </p>
                </div>
              )}

              {pendingRequest.status === 'Completed' &&
                pendingRequest.conversationsDeleted !== undefined && (
                  <div className="pt-2 border-t border-gray-200">
                    <CheckCircle className="w-5 h-5 text-green-600 inline mr-2" />
                    <span className="text-sm text-gray-900">
                      {pendingRequest.conversationsDeleted} conversations deleted on{' '}
                      {formatDate(pendingRequest.completedAt!)}
                    </span>
                  </div>
                )}
            </div>

            {pendingRequest.status === 'Pending' && (
              <button
                onClick={cancelDeletionRequest}
                disabled={loading}
                className="w-full flex items-center justify-center px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
              >
                <XCircle className="w-4 h-4 mr-2" />
                Cancel Deletion Request
              </button>
            )}
          </div>
        ) : (
          <div className="space-y-4">
            {!showConfirmation ? (
              <>
                <div className="bg-yellow-50 border border-yellow-200 rounded-md p-4">
                  <div className="flex items-start">
                    <AlertTriangle className="w-5 h-5 text-yellow-600 mt-0.5 mr-3 flex-shrink-0" />
                    <div className="text-sm text-yellow-800">
                      <p className="font-medium mb-2">
                        About Data Deletion (GDPR Right to be Forgotten)
                      </p>
                      <ul className="list-disc list-inside space-y-1">
                        <li>Your conversation history will be permanently deleted</li>
                        <li>Deletion will be processed in 30 days</li>
                        <li>You can cancel this request at any time before processing</li>
                        <li>Audit logs will be retained for compliance (7 years)</li>
                        <li>This action cannot be undone once processing is complete</li>
                      </ul>
                    </div>
                  </div>
                </div>

                <button
                  onClick={() => setShowConfirmation(true)}
                  className="w-full flex items-center justify-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-red-600 hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500"
                >
                  <Trash2 className="w-4 h-4 mr-2" />
                  Request Data Deletion
                </button>
              </>
            ) : (
              <div className="bg-red-50 border border-red-200 rounded-md p-4 space-y-4">
                <div className="flex items-start">
                  <AlertTriangle className="w-6 h-6 text-red-600 mt-0.5 mr-3 flex-shrink-0" />
                  <div>
                    <h4 className="text-base font-semibold text-red-900 mb-2">
                      Confirm Data Deletion Request
                    </h4>
                    <p className="text-sm text-red-800 mb-3">
                      Are you absolutely sure? Your conversation data will be permanently deleted in
                      30 days. This action will be logged and cannot be undone after processing.
                    </p>
                  </div>
                </div>

                <div className="flex gap-3">
                  <button
                    onClick={submitDeletionRequest}
                    disabled={loading}
                    className="flex-1 px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-red-600 hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 disabled:opacity-50"
                  >
                    {loading ? 'Submitting...' : 'Yes, Delete My Data'}
                  </button>
                  <button
                    onClick={() => setShowConfirmation(false)}
                    disabled={loading}
                    className="flex-1 px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        <div className="mt-6 pt-6 border-t border-gray-200">
          <p className="text-xs text-gray-500">
            Your deletion request will be processed per GDPR regulations (FR-014b, FR-014c). Audit
            logs are retained for 7 years as required by law (FR-014d). You will receive email
            confirmations at each step of the process.
          </p>
        </div>
      </div>
    </div>
  );
};

export default DataDeletionRequest;
