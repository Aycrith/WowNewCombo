using System.Collections.Generic;
using System.Linq;

namespace Core.Session;

public interface IGrindSessionDAO
{
    IQueryable<GrindSession> Load();
    void Save(GrindSession session);
}