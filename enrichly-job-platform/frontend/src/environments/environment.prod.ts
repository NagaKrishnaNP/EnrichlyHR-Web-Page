// In the Docker build, nginx substitutes API_BASE_URL at container start (see docker
// entrypoint script). This default is only used for a plain `ng build --configuration production`
// run outside Docker.
export const environment = {
  production: true,
  apiBaseUrl: (window as any).__env?.apiBaseUrl || 'http://localhost:5000/api'
};
