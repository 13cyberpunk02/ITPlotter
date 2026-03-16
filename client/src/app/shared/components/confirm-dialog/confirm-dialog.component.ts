import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  title = input.required<string>();
  message = input.required<string>();
  confirmLabel = input('Confirm');
  cancelLabel = input('Cancel');

  confirmed = output<void>();
  cancelled = output<void>();
}
