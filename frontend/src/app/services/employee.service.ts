import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Employee } from '../models/leave-request.model';
import { API_BASE_URL } from './api';

@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private url = `${API_BASE_URL}/employees`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Employee[]> {
    return this.http.get<Employee[]>(this.url);
  }
}
