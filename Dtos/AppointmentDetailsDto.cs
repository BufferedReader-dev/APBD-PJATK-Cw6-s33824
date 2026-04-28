using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hospital.Dtos;

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public int IdPatient { get; set;}
    public int IdDoctor { get; set;}
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PatientFullName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhoneNumber { get; set; } = string.Empty;
    public string DoctorLicenceNumber { get; set; } = string.Empty;
    public string? InternalNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}