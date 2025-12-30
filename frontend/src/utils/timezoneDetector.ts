/**
 * Browser timezone detection utility
 * Detects user's IANA timezone identifier for accurate timestamp handling
 */

/**
 * Gets the user's IANA timezone identifier (e.g., "America/New_York", "Europe/London")
 * @returns IANA timezone identifier or "UTC" as fallback
 */
export function getUserTimezone(): string {
  try {
    // Modern browsers support Intl.DateTimeFormat().resolvedOptions().timeZone
    const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;

    if (timezone && isValidTimezone(timezone)) {
      return timezone;
    }
  } catch (error) {
    console.warn('Failed to detect timezone:', error);
  }

  // Fallback to UTC if detection fails
  return 'UTC';
}

/**
 * Validates if a timezone identifier is valid IANA format
 * @param timezone - Timezone string to validate
 * @returns true if valid, false otherwise
 */
function isValidTimezone(timezone: string): boolean {
  try {
    // Try to format a date with the timezone - will throw if invalid
    new Intl.DateTimeFormat('en-US', { timeZone: timezone });
    return true;
  } catch {
    return false;
  }
}

/**
 * Gets timezone offset in minutes from UTC
 * @returns Offset in minutes (e.g., -300 for EST, 60 for CET)
 */
export function getTimezoneOffset(): number {
  return new Date().getTimezoneOffset();
}

/**
 * Formats timezone offset as string (e.g., "UTC-5", "UTC+1")
 * @returns Formatted offset string
 */
export function getTimezoneOffsetString(): string {
  const offset = getTimezoneOffset();
  const hours = Math.floor(Math.abs(offset) / 60);
  const minutes = Math.abs(offset) % 60;
  const sign = offset > 0 ? '-' : '+'; // Note: offset is negative for positive UTC

  if (minutes === 0) {
    return `UTC${sign}${hours}`;
  }
  return `UTC${sign}${hours}:${minutes.toString().padStart(2, '0')}`;
}

/**
 * Gets full timezone information for debugging
 * @returns Object with timezone details
 */
export function getTimezoneInfo() {
  const timezone = getUserTimezone();
  const offset = getTimezoneOffset();
  const offsetString = getTimezoneOffsetString();

  return {
    timezone,
    offset,
    offsetString,
    formatted: `${timezone} (${offsetString})`,
  };
}
