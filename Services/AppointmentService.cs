using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using Hospital.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Console;

namespace Hospital.Services;
using Dtos;
public class AppointmentService(IConfiguration configuration) : IAppointmentService
{
    public async Task<IEnumerable<AppointmentListDto>> GetBasicAllAsync(string? status, string? patientLastName, CancellationToken cancellationToken = default)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine("Breakpoint 1");
        await using var connection = new SqlConnection(connectionString);
        Console.WriteLine("Breakpoint 2");
        await connection.OpenAsync(cancellationToken);
        //To start transaction connetion must be established
        Console.WriteLine("Breakpoint 2.1");

        await using SqlCommand command = connection.CreateCommand();
        Console.WriteLine("Breakpoint 2.2");
        command.Connection = connection;
        command.CommandText = """
                              SELECT
                                  a.IdAppointment,
                                  a.AppointmentDate,
                                  a.Status,
                                  a.Reason,
                                  p.FirstName + N' ' + p.LastName AS PatientFullName,
                                  p.Email AS PatientEmail
                              FROM dbo.Appointments a
                              JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                              WHERE (@Status IS NULL OR a.Status = @Status)
                                AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                              ORDER BY a.AppointmentDate;
                              """;
        command.Parameters.AddWithValue("@Status", status ?? (object)DBNull.Value);
        Console.WriteLine("Breakpoint 3");
        command.Parameters.AddWithValue("@PatientLastName", patientLastName ?? (object)DBNull.Value);
        Console.WriteLine("Breakpoint 4");

        //await unwraps Task<SqlDataReader> to SqlDataReader.
        //That is why i can write like that:
        Console.WriteLine("Breakpoint 5");
       await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
       Console.WriteLine("Breakpoint 6");
       var result = new List<AppointmentListDto>();
       while (await reader.ReadAsync(cancellationToken))
       {
           var newEl = new AppointmentListDto();
           newEl.IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment"));
           newEl.AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate"));
           newEl.Status = reader.GetString(reader.GetOrdinal("Status"));
           newEl.Reason = reader.GetString(reader.GetOrdinal("Reason"));
           newEl.PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName"));
           newEl.PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"));

           result.Add(newEl);
       }
       command.Parameters.Clear();
       Console.WriteLine("Breakpoint 7");

       
       Console.WriteLine("Breakpoint 8");
        
       
       return result;
    }

    public async Task<AppointmentDetailsDto> GetDetailedByIdAsync(int id, CancellationToken token)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(token);

        await using SqlCommand command = connection.CreateCommand();

        command.Connection = connection;

        command.CommandText = """
                               SELECT
                                  a.IdAppointment,
                                  a.IdPatient,
                                  a.IdDoctor,
                                  a.AppointmentDate,
                                  a.Status,
                                  a.Reason,
                                  p.FirstName + N' ' + p.LastName AS PatientFullName,
                                  p.Email AS PatientEmail,
                                  p.PhoneNumber,
                                  d.LicenseNumber,
                                  a.InternalNotes,
                                  a.CreatedAt
                              FROM dbo.Appointments a
                                       JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                       JOIN dbo.Doctors d on d.IdDoctor = a.IdDoctor
                              WHERE @Id = a.IdAppointment
                              ORDER BY a.AppointmentDate
                              """;

        command.Parameters.AddWithValue("@Id", id);
       await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
        bool hasRecords = await reader.ReadAsync(token);

        if (!hasRecords) throw new NoSuchIndexException("There is no appointment with index " + id); 
        
        var result = new AppointmentDetailsDto();
        result.IdAppointment = reader.GetInt32(0);
        result.IdPatient = reader.GetInt32(1);
        result.IdDoctor = reader.GetInt32(2);
        result.AppointmentDate = reader.GetDateTime(3);
        result.Status = reader.GetString(4);
        result.Reason = reader.GetString(5);
        result.PatientFullName = reader.GetString(6);
        result.PatientEmail = reader.GetString(7);
        result.PatientPhoneNumber = reader.GetString(8);
        result.DoctorLicenceNumber = reader.GetString(9);

        if (reader.IsDBNull(10))
            result.InternalNotes = null;
        else
        result.InternalNotes = reader.GetString(10);
        
        Console.WriteLine(result.InternalNotes);

        result.CreatedAt = reader.GetDateTime(11);
        Console.WriteLine(result.CreatedAt);


        command.Parameters.Clear();
        return result;
    }

    public async Task AddAppointmentAsync(CreateAppointmentRequestDto appointmentDto, CancellationToken token)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(token);


        await using SqlCommand command = new SqlCommand();
        command.Connection = connection;
        
        DbTransaction transaction = await connection.BeginTransactionAsync();
        command.Transaction = (SqlTransaction)transaction;
        
        //Business rule Patient 
        {
            await ValidatePerson(appointmentDto.IdPatient, "Patient", "Patients",
                command, transaction, token);
        }
        //
        {
            //Business rule Doctor
            await ValidatePerson(appointmentDto.IdDoctor, "Doctor", "Doctors",
                command, transaction, token);
        }
        //
        
        //Date is not in the past business rule 
        if (appointmentDto.AppointmentDate <= DateTime.Now)
        {
            await transaction.RollbackAsync(token);
            throw new InappropriateDateException("The appointment time cannot be in the past");
        }
        //
        
        //Appointment description business rule
        //nie dodawalem tej reguly, bo w przykladowym json-ie w opisie projektu nie jest wskazana mozliwosc dodawania Internal Notes,
        //dodatkowo Internal Notes jest polem nullowalnym.
        
        
        //Doctor has no overlapping reservations business rule
        var check = await DoctorHasAnotherAppThatTime(appointmentDto.AppointmentDate, appointmentDto.IdDoctor, null, command, token);
        if (check)
        {
            await transaction.RollbackAsync(token);
            throw new InappropriateDateException("Doctor has another appointment that time");
        }
        //
        command.CommandText = """
                              insert into dbo.Appointments 
                              (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                              values (@IdPatient, @IdDoctor,@AppointmentDate,@Status, @Reason);
                              """;
        
        command.Parameters.Clear();
        
        command.Parameters.AddWithValue("@IdPatient", appointmentDto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", appointmentDto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", appointmentDto.AppointmentDate);
        command.Parameters.AddWithValue("@Status", "Scheduled");
        command.Parameters.AddWithValue("@Reason", appointmentDto.Reason);
       await command.ExecuteNonQueryAsync(token);
         await transaction.CommitAsync(token);
    }
    //I suppose, that appointments last 1 hour, because it was not specified in the project description.

    private async Task ValidatePerson(int id, string person, string tab, SqlCommand command, 
        DbTransaction transaction, CancellationToken token)
    {
        //try-catch for a programmer.
        try
        {
            command.Parameters.Clear();

            command.CommandText = $"select IsActive from {tab} where @PersonId = Id{person}";
            command.Parameters.AddWithValue($"@PersonId", id);

            Console.WriteLine(command.CommandText);
            object? isActive = await command.ExecuteScalarAsync(token);
            if (isActive == null)
            {
                await transaction.RollbackAsync(token);
                throw new NoSuchPatientException($"No {person} with id " + id);
            }
            else if (!(bool)isActive)
            {
                await transaction.RollbackAsync(token);
                throw new PatientIsNotActiveException($"{person} with id {id} is not active");
            }

            command.Parameters.Clear();
        }catch (SqlException e)
        {
            throw new ArgumentException("Wrong name for a table or a record");
        }

    }
    private async Task<bool> DoctorHasAnotherAppThatTime(DateTime appTime, int doctorId, int? appId, SqlCommand command, CancellationToken token)
    {
            command.Parameters.Clear();
            if (appId is null)
            {
                command.CommandText = """
                                      select 1 from Appointments
                                      where IdDoctor = @IdDoctor and @Date between AppointmentDate
                                          and DateAdd(hour, 1, AppointmentDate)
                                      """;
                Console.WriteLine(appTime.AddHours(1));
            }
            else
            {
                command.CommandText = """
                                      select 1 from Appointments
                                      where @IdAppointment <> IdAppointment and IdDoctor = @IdDoctor and @Date between AppointmentDate
                                          and DateAdd(hour, 1, AppointmentDate)
                                      """;
                command.Parameters.AddWithValue("@IdAppointment", appId);
                Console.WriteLine("Called from update");
            }

            command.Parameters.AddWithValue("@Date", appTime);
            command.Parameters.AddWithValue("@IdDoctor", doctorId);

            object? res = await command.ExecuteScalarAsync(token);
            return res != null;
    
    }

    
    

    public async Task UpdateAsync(UpdateAppointmentRequestDto updateDto, CancellationToken token)
    {
       string? connectionString = configuration.GetConnectionString("DefaultConnection");
        
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(token);
        
        await using SqlCommand command = new SqlCommand();
        command.Connection = connection;
        
        DbTransaction transaction = await connection.BeginTransactionAsync(token);
        command.Transaction = (SqlTransaction)transaction;
        
        //Appointment exists business rule
       await AppointExists(updateDto.IdAppointment, command, transaction, token);
        //
        
        //Business rule Patient 
            await ValidatePerson(updateDto.IdPatient, "Patient", "Patients",
                command, transaction, token);
        //
            //Business rule Doctor
            await ValidatePerson(updateDto.IdDoctor, "Doctor", "Doctors",
                command, transaction, token);
        //
        
        //Doctor has no overlapping reservations business rule
        var check = await DoctorHasAnotherAppThatTime(updateDto.AppointmentDate, updateDto.IdDoctor, updateDto.IdAppointment,
            command, token);
        if (check)
        {
            await transaction.RollbackAsync(token);
            throw new InappropriateDateException("Doctor has another appointment that time");
        }
        //
        
        //Business rule status
        ValidateStatus(updateDto.Status);
        //
        
        //Date is not in the past business rule 
        if (updateDto.AppointmentDate <= DateTime.Now)
        {
            await transaction.RollbackAsync(token);
            throw new InappropriateDateException("The appointment time cannot be in the past");
        }
        //
        
        //Cannot change Completed appointment business rule
        if (updateDto.Status != "Completed")
        {
            command.Parameters.Clear();
            command.CommandText = """select Status from Appointments where IdAppointment = @IdAppointment""";
            command.Parameters.AddWithValue("@IdAppointment", updateDto.IdAppointment);

             var commandRes = await command.ExecuteScalarAsync(token); 
            Console.WriteLine((string)commandRes);
            if ((string)commandRes == "Completed")
            {
                await transaction.RollbackAsync(token);
                throw new ArgumentException("The completed appointment's status cannot be changed");
            }
        }
        
        //
        
        
        
        command.CommandText = """
                              update dbo.Appointments 
                                  set
                              IdPatient = @IdPatient,
                              IdDoctor = @IdDoctor,
                              AppointmentDate = @AppointmentDate,
                              Status = @Status,
                              Reason = @Reason,
                              InternalNotes = @InternalNotes
                              where IdAppointment = @IdAppointment
                              """;
        
        command.Parameters.Clear();

        command.Parameters.AddWithValue("@IdAppointment", updateDto.IdAppointment);
        
        command.Parameters.AddWithValue("@IdPatient", updateDto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", updateDto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", updateDto.AppointmentDate);
        command.Parameters.AddWithValue("@Status", updateDto.Status);
        command.Parameters.AddWithValue("@Reason", updateDto.Reason);
        command.Parameters.AddWithValue("@InternalNotes", updateDto.InternalNotes);

       await command.ExecuteNonQueryAsync(token);
         await transaction.CommitAsync(token);

    }

    public void ValidateStatus(string status)
    {
        HashSet<string> possibleStatuses = [ "Scheduled", "Cancelled", "Completed" ];
        if (!possibleStatuses.Contains(status))
        {
            throw new ArgumentException("Invalid status");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken token)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(token);
        
        await using SqlCommand command = new SqlCommand();
        command.Connection = connection;
        
        DbTransaction transaction = await connection.BeginTransactionAsync(token);
        command.Transaction = (SqlTransaction)transaction;

        await AppointExists(id, command, transaction, token);
        
        
    }


    public async Task AppointExists(int id, SqlCommand command, DbTransaction transaction, CancellationToken token)
    {
        //Appointment exists business rule
        command.CommandText = "select 1 from Appointments where IdAppointment = @IdAppointment";
        int appId = id;
        Console.WriteLine("Num: " + appId);
        command.Parameters.AddWithValue("@IdAppointment", appId);
        object? commandRes = await command.ExecuteScalarAsync(token);
        Console.WriteLine(commandRes);
        if (commandRes == null)
        {
            throw new NoSuchAppointmentException($"Appointment with id {appId} does not exist");
        }
        //
        
        command.Parameters.Clear();
        command.CommandText = """select Status from Appointments where IdAppointment = @IdAppointment""";
        command.Parameters.AddWithValue("@IdAppointment", id);

        commandRes = await command.ExecuteScalarAsync(token); 
        Console.WriteLine((string)commandRes);
        if ((string)commandRes == "Completed")
        {
            await transaction.RollbackAsync(token);
            throw new ArgumentException("A completed appointment cannot be deleted");
        }
    }
}

//Ended: 
//Adding new appointment works, need to add business logic, test it
//and add transactions.