import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-login',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  email = '';
  password = '';
  loading = signal(false);

  onSubmit(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.auth.loadProfile().subscribe();
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.toast.error('Invalid email or password');
        this.loading.set(false);
      },
    });
  }
}
