import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../models/api.models';

/** A normalised API error: a user-facing message plus optional field errors keyed by camelCase path. */
export interface ApiError {
  status: number;
  message: string;
  code?: string;
  fieldErrors: Record<string, string[]>;
}

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, message: 'Something went wrong. Please try again.', fieldErrors: {} };
  }

  if (error.status === 0) {
    return { status: 0, message: 'The server could not be reached. Check your connection and try again.', fieldErrors: {} };
  }

  const problem = (typeof error.error === 'object' && error.error ? error.error : {}) as ProblemDetails;
  const fieldErrors = problem.errors ?? {};
  const firstFieldError = Object.values(fieldErrors)[0]?.[0];

  return {
    status: error.status,
    code: problem.code,
    message: firstFieldError ?? problem.title ?? defaultMessage(error.status),
    fieldErrors,
  };
}

function defaultMessage(status: number): string {
  switch (status) {
    case 401: return 'Please sign in again.';
    case 403: return 'You do not have permission to do this.';
    case 404: return 'The requested item was not found.';
    case 429: return 'Too many requests. Please wait a moment.';
    case 503: return 'The service is temporarily unavailable.';
    default: return 'Something went wrong. Please try again.';
  }
}
