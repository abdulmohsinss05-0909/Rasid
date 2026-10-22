/**
 * Every API route used by the app, in one place. Services compose `apiBaseUrl + path`;
 * no URL string appears anywhere else in the frontend.
 */
export const ApiEndpoints = {
  auth: {
    login: 'auth/login',
    refresh: 'auth/refresh',
    logout: 'auth/logout',
    me: 'auth/me',
  },
  topics: {
    list: 'topics',
    byId: (id: string) => `topics/${id}`,
    pause: (id: string) => `topics/${id}/pause`,
    resume: (id: string) => `topics/${id}/resume`,
    refresh: (id: string) => `topics/${id}/refresh`,
    previewQuery: 'topics/preview-query',
    dashboard: (id: string) => `topics/${id}/dashboard`,
    posts: (id: string) => `topics/${id}/posts`,
    insights: (id: string) => `topics/${id}/insights`,
  },
  overview: 'overview',
  alerts: 'alerts',
  system: {
    status: 'system/status',
  },
} as const;
