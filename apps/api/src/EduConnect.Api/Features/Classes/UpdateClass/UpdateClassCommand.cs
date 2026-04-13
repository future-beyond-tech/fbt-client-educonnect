namespace EduConnect.Api.Features.Classes.UpdateClass;

public record UpdateClassCommand(
    Guid Id,
    string Name,
    string Section,
    string AcademicYear) : IRequest<UpdateClassResponse>;

public record UpdateClassResponse(Guid ClassId, string Message);
