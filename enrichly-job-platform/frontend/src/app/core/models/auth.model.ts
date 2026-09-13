export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  email: string;
  displayName: string;
  userId: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}
