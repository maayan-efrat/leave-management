using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Api.Models;

// Incoming payload for creating a leave request.
public class CreateLeaveRequestDto
{
    public int EmployeeId { get; set; }

    // JSON accepts any number for an enum, so reject values that are not a LeaveType.
    [EnumDataType(typeof(LeaveType))]
    public LeaveType Type { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
