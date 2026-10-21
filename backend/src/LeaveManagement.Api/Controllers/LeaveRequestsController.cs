using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagement.Api.Controllers;

// HTTP only: the rules and data access live in LeaveRequestService.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveRequestService _service;

    public LeaveRequestsController(LeaveRequestService service)
    {
        _service = service;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_service.GetAll());
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        return Ok(_service.Search(name));
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        return ToActionResult(_service.Create(dto));
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id}/approve")]
    public IActionResult Approve(int id)
    {
        return ToActionResult(_service.Approve(id));
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        return result.Status switch
        {
            ServiceStatus.Ok => Ok(result.Value),
            ServiceStatus.NotFound => NotFound(result.Error),
            ServiceStatus.Conflict => Conflict(result.Error),
            _ => BadRequest(result.Error)
        };
    }
}
