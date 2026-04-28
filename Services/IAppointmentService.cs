namespace Hospital.Services;
using Dtos;
public interface IAppointmentService
{
    public Task<IEnumerable<AppointmentListDto>> GetBasicAllAsync(string? status, string? patientLastName,
        CancellationToken cancellationToken = default);

    public Task<AppointmentDetailsDto> GetDetailedByIdAsync(int id, CancellationToken token);
    public Task AddAppointmentAsync(CreateAppointmentRequestDto appointmentDto, CancellationToken token);
    public Task UpdateAsync(UpdateAppointmentRequestDto updateDto, CancellationToken token);
    public Task DeleteAsync(int id, CancellationToken token);
}