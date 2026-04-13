namespace EduConnect.Api.Features.Classes.CreateClass;

public record CreateClassCommand(
    string Name,
    string Section,
    string AcademicYear) : IRequest<CreateClassResponse>;

public record CreateClassResponse(Guid ClassId, string Message);
