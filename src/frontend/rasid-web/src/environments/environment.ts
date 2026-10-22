/**
 * Development environment. The API is reached through the dev-server proxy (proxy.conf.json),
 * so the browser talks to one origin and the refresh cookie stays first-party.
 */
export const environment = {
  name: 'Development',
  production: false,
  apiBaseUrl: '/api/v1',
};
