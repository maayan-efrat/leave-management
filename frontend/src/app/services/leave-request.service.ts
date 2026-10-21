import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateLeaveRequest, LeaveRequest } from '../models/leave-request.model';
import { API_BASE_URL } from './api';

@Injectable({ providedIn: 'root' })
export class LeaveRequestService {
  private url = `${API_BASE_URL}/leave-requests`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<LeaveRequest[]> {
    return this.http.get<LeaveRequest[]>(this.url);
  }

  create(request: CreateLeaveRequest): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(this.url, request);
  }

  approve(id: number): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(`${this.url}/${id}/approve`, {});
  }
}
