import { Component, DestroyRef, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, finalize, of, switchMap, tap } from 'rxjs';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import {
  CreateLeaveRequest,
  Employee,
  LeaveRequest,
  LeaveStatus,
  LeaveType,
  STATUS_LABELS,
  TYPE_LABELS
} from '../models/leave-request.model';
import { EmployeeService } from '../services/employee.service';
import { LeaveRequestService } from '../services/leave-request.service';

// Number of calendar days between two 'yyyy-MM-dd' values, inclusive (matches the API).
// Parsed as UTC so local time zones and DST cannot shift the result by a day.
function daysBetween(start: string, end: string): number {
  const toUtc = (s: string) => {
    const [y, m, d] = s.split('-').map(Number);
    return Date.UTC(y, m - 1, d);
  };
  return Math.round((toUtc(end) - toUtc(start)) / 86_400_000) + 1;
}

// Start must not be after end; this also rules out a negative number of days.
function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value;
  const end = group.get('endDate')?.value;
  if (!start || !end) return null;
  return daysBetween(start, end) < 1 ? { dateRange: true } : null;
}

@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit {
  // Exposed so the template can compare against names instead of 0/1/2.
  readonly LeaveType = LeaveType;
  readonly LeaveStatus = LeaveStatus;

  requests: LeaveRequest[] = [];
  loading = false;
  // Each next() reloads the list.
  private refresh$ = new Subject<void>();

  employees: Employee[] = [];
  submitting = false;
  submitError: string | null = null;

  // Per row, so approving one request does not lock or hide the others.
  approving = new Set<number>();
  approveErrors = new Map<number, string>();
  // Rows that show a success message until the user closes it.
  approved = new Set<number>();

  // Type starts as null, not 0: 0 is Vacation, so a 0 default would count as "chosen".
  form = new FormGroup(
    {
      employeeId: new FormControl<number | null>(null, Validators.required),
      type: new FormControl<LeaveType | null>(null, Validators.required),
      startDate: new FormControl<string>('', Validators.required),
      endDate: new FormControl<string>('', Validators.required)
    },
    { validators: dateRangeValidator }
  );

  constructor(
    private leaveRequests: LeaveRequestService,
    private employeeService: EmployeeService,
    private destroyRef: DestroyRef
  ) {}

  ngOnInit(): void {
    // switchMap drops an older response, so a slow reload cannot overwrite a newer list.
    this.refresh$
      .pipe(
        tap(() => (this.loading = true)),
        switchMap(() =>
          // On failure keep the current list; the spinner must still stop.
          this.leaveRequests.getAll().pipe(catchError(() => of(this.requests)))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((data) => {
        this.requests = data;
        this.loading = false;
      });
    this.load();

    this.employeeService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((data) => {
        this.employees = data;
      });
  }

  // Shown only after the user touched the field, so a fresh form is not all red.
  showError(name: string): boolean {
    const control = this.form.get(name)!;
    return control.invalid && control.touched;
  }

  get showDateRangeError(): boolean {
    return this.form.hasError('dateRange') && !!this.form.get('endDate')?.touched;
  }

  get days(): number | null {
    const { startDate, endDate } = this.form.value;
    return startDate && endDate ? daysBetween(startDate, endDate) : null;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // The form is valid here, so the required fields are not null.
    const { employeeId, type, startDate, endDate } = this.form.getRawValue();
    const request: CreateLeaveRequest = {
      employeeId: employeeId!,
      type: type!,
      startDate: startDate!,
      endDate: endDate!
    };

    this.submitting = true;
    this.submitError = null;
    this.leaveRequests
      .create(request)
      .pipe(
        finalize(() => (this.submitting = false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: () => {
          this.form.reset();
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          // Business errors come back as plain text; anything else gets a generic message.
          this.submitError = typeof err.error === 'string' ? err.error : 'Could not submit the request';
        }
      });
  }

  load(): void {
    this.refresh$.next();
  }

  approve(request: LeaveRequest): void {
    this.approving.add(request.id);
    this.approveErrors.delete(request.id);
    this.leaveRequests
      .approve(request.id)
      .pipe(
        finalize(() => this.approving.delete(request.id)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (updated) => {
          // Update only this row from the response instead of reloading the whole list.
          request.status = updated.status;
          this.approved.add(request.id);
        },
        error: (err: HttpErrorResponse) => {
          // Business errors (already approved, not enough balance) come back as plain text.
          this.approveErrors.set(
            request.id,
            typeof err.error === 'string' ? err.error : 'Could not approve the request'
          );
        }
      });
  }

  // 'Unknown' for values the API sends but this enum does not know (e.g. old rows saved before validation).
  typeLabel(type: LeaveType): string {
    return TYPE_LABELS[type] ?? 'Unknown';
  }

  statusLabel(status: LeaveStatus): string {
    return STATUS_LABELS[status] ?? 'Unknown';
  }
}
