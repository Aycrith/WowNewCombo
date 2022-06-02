using System.Collections.Generic;

namespace Core.Session
{
    public interface IGrindSessionDAO
    {
        IEnumerable<GrindSession> Load();
        void Save(GrindSession session);
    }
}
