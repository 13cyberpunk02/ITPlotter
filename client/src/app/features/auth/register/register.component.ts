import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-register',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  firstName = '';
  lastName = '';
  email = '';
  password = '';
  loading = signal(false);

  onSubmit(): void {
    if (!this.firstName || !this.lastName || !this.email || !this.password) return;
    this.loading.set(true);
    this.auth
      .register({
        firstName: this.firstName,
        lastName: this.lastName,
        email: this.email,
        password: this.password,
      })
      .subscribe({
        next: () => {
          this.auth.loadProfile().subscribe();
          this.router.navigate(['/dashboard']);
        },
        error: () => {
          this.toast.error('Registration failed. Email may already be in use.');
          this.loading.set(false);
        },
      });
  }
}
