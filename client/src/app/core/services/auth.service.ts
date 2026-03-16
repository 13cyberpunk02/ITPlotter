import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  RefreshTokenRequest,
  UserDto,
} from '../models/auth.models';

const TOKEN_KEY = 'itplotter-access-token';
const REFRESH_KEY = 'itplotter-refresh-token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  readonly user = signal<UserDto | null>(null);
  readonly isAuthenticated = computed(() => !!this.getToken());

  register(request: RegisterRequest) {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/register`, request)
      .pipe(tap(res => this.storeTokens(res)));
  }

  login(request: LoginRequest) {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, request)
      .pipe(tap(res => this.storeTokens(res)));
  }

  refresh() {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return of(null);
    const body: RefreshTokenRequest = { refreshToken };
    return this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, body).pipe(
      tap(res => this.storeTokens(res)),
      catchError(() => {
        this.logout();
        return of(null);
      })
    );
  }

  loadProfile() {
    return this.http.get<UserDto>(`${this.baseUrl}/profile`).pipe(
      tap(user => this.user.set(user)),
      catchError(() => {
        this.logout();
        return of(null);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    this.user.set(null);
    this.router.navigate(['/auth/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  private storeTokens(res: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
  }
}
