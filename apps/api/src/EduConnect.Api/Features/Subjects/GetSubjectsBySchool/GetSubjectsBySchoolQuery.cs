namespace EduConnect.Api.Features.Subjects.GetSubjectsBySchool;

public record GetSubjectsBySchoolQuery() : IRequest<List<SubjectDto>>;

public record SubjectDto(Guid Id, string Name);
