// Same values as the API enums, which are sent over JSON as numbers.
export enum LeaveType {
  Vacation = 0,
  Sick = 1,
  Unpaid = 2
}

export enum LeaveStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2
}

// Display text per value; Record makes the compiler require a label for every value.
export const TYPE_LABELS: Record<LeaveType, string> = {
  [LeaveType.Vacation]: 'Vacation',
  [LeaveType.Sick]: 'Sick',
  [LeaveType.Unpaid]: 'Unpaid'
};

export const STATUS_LABELS: Record<LeaveStatus, string> = {
  [LeaveStatus.Pending]: 'Pending',
  [LeaveStatus.Approved]: 'Approved',
  [LeaveStatus.Rejected]: 'Rejected'
};

export interface Employee {
  id: number;
  name: string;
  annualQuota: number;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  // Populated by the API on reads; absent on the response to a create.
  employee?: Employee;
  type: LeaveType;
  startDate: string;
  endDate: string;
  status: LeaveStatus;
  days: number;
}

// Body of POST /api/leave-requests.
export interface CreateLeaveRequest {
  employeeId: number;
  type: LeaveType;
  startDate: string;
  endDate: string;
}
