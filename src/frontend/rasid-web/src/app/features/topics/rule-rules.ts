import { RuleType } from '../../core/models/api.models';

/**
 * Client-side mirror of the server's rule rules, for instant feedback while typing.
 * The API remains the authority and re-validates everything.
 */
export function normalizeRule(type: RuleType, raw: string): { value: string } | { error: string } {
  const value = raw.trim().replace(/\s+/g, ' ');

  switch (type) {
    case 'Hashtag': {
      const tag = value.replace(/^#+/, '');
      return /^[\p{L}\p{N}_]{1,100}$/u.test(tag)
        ? { value: tag }
        : { error: 'Hashtags use letters, numbers or underscores, with no spaces.' };
    }
    case 'Account': {
      const handle = value.replace(/^@+/, '');
      return /^[A-Za-z0-9_]{1,15}$/.test(handle)
        ? { value: handle }
        : { error: 'Accounts are X usernames: up to 15 letters, numbers or underscores.' };
    }
    default:
      if (value.length < 2 || value.length > 100) {
        return { error: 'Keywords must be 2–100 characters.' };
      }
      return /^[\p{L}\p{N}][\p{L}\p{N} .,'&_-]*$/u.test(value)
        ? { value }
        : { error: "Keywords may contain letters, numbers, spaces and . , ' & - _ only." };
  }
}

export function displayRule(type: RuleType, value: string): string {
  return type === 'Hashtag' ? `#${value}` : type === 'Account' ? `@${value}` : value;
}
