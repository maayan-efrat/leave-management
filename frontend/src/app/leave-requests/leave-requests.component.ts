import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { Employee } from '../models/leave-request.model';

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

// NOTE: This component was written quickly for a POC.
// It talks to the API directly, manages state by hand and uses `any` everywhere.
@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit {
  requests: any[] = [];
  loading = false;

  employees: Employee[] = [];
  submitting = false;
  submitError: string | null = null;

  // Type starts as null, not 0: 0 is Vacation, so a 0 default would count as "chosen".
  form = new FormGroup(
    {
      employeeId: new FormControl<number | null>(null, Validators.required),
      type: new FormControl<number | null>(null, Validators.required),
      startDate: new FormControl<string>('', Validators.required),
      endDate: new FormControl<string>('', Validators.required)
    },
    { validators: dateRangeValidator }
  );

  private apiUrl = 'http://localhost:5080/api/leave-requests';
  private employeesUrl = 'http://localhost:5080/api/employees';

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.load();
    this.http.get<Employee[]>(this.employeesUrl).subscribe((data) => {
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

    this.submitting = true;
    this.submitError = null;
    this.http.post(this.apiUrl, this.form.value).subscribe({
      next: () => {
        this.submitting = false;
        this.form.reset();
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        // Business errors come back as plain text; anything else gets a generic message.
        this.submitError = typeof err.error === 'string' ? err.error : 'Could not submit the request';
      }
    });
  }

  load(): void {
    this.loading = true;
    this.http.get<any>(this.apiUrl).subscribe((data) => {
      this.requests = data;
      this.loading = false;
    });
  }

  // Wired up by the candidate as part of the assignment.
  approve(id: number): void {
    // TODO (candidate): call POST /api/leave-requests/{id}/approve
    // and handle loading / error / success without a generic alert.
    this.http.post<any>(this.apiUrl + '/' + id + '/approve', {}).subscribe(() => {
      this.load();
    });
  }

  typeLabel(type: number): string {
    if (type == 0) return 'Vacation';
    if (type == 1) return 'Sick';
    return 'Unpaid';
  }

  statusLabel(status: number): string {
    if (status == 0) return 'Pending';
    if (status == 1) return 'Approved';
    return 'Rejected';
  }
}
