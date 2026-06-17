using StarterApp.API.Models.Entities;
using StarterApp.API.Models.QueryParameters;

namespace StarterApp.API.Data.Repositories;

/// <summary>
/// Repository interface for note data access.
/// </summary>
public interface INoteRepository : IRepository<Note, NoteQueryParameters>
{
}
