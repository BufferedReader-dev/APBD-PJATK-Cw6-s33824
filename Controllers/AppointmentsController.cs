using System.ComponentModel.Design.Serialization;
using Hospital.Dtos;
using Hospital.Exceptions;
using Hospital.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hospital.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBasicAllAsync(
        [FromQuery]string? status, [FromQuery]string? patientLastName, CancellationToken token)
    {
        return Ok(await service.GetBasicAllAsync(status, patientLastName, token));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetailedAppointById([FromRoute] int id, CancellationToken token)
    {
         AppointmentDetailsDto result;
        try
        {
            var val = await service.GetDetailedByIdAsync(id, token);
            result = val;
        }
        catch (NoSuchIndexException e)
        {
            return NotFound("The appointment with id " + id + " was not found");
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult> AddNewAppoint(
        [FromBody]CreateAppointmentRequestDto dto,
        CancellationToken token)
    {
        try
        {
            await service.AddAppointmentAsync(dto, token);
        }
        catch (Exception e)
        {
            return Conflict(e.Message);
        }

        return Created();
    }


    [HttpPut]
    public async Task<IActionResult> UpdateAppoint([FromBody] UpdateAppointmentRequestDto dto, CancellationToken token)
    {
        try
        {
            await service.UpdateAsync(dto, token);
        }
        catch (NoSuchAppointmentException e)
        {
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            return Conflict(e.Message);
        }

        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAppoint([FromRoute] int id, CancellationToken token)
    {
        try
        {
            await service.DeleteAsync(id, token);
        }
        catch (ArgumentException e)
        {
            return Conflict(e.Message);
        }
        catch (NoSuchAppointmentException e)
        {
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            return Conflict();
        }
        return NoContent();
    }
        
}