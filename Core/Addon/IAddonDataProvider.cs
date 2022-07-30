namespace Core
{
    public interface IAddonDataProvider
    {
        void Update();
        void InitFrames(DataFrame[] frames);

        int GetInt(int index);
        float GetFixed(int index);
        string GetString(int index);

        void Dispose();
    }
}
