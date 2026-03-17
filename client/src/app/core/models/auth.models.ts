export type UserRole = 'User' | 'Admin';

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpires: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface UserDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  photoPath: string | null;
  role: UserRole;
}
