using Core;

namespace CoreTests
{
    public class MockMouseOverReader : IMouseOverReader
    {
        public int MouseOverLevel => throw new System.NotImplementedException();

        public UnitClassification MouseOverClassification => throw new System.NotImplementedException();

        public int MouseOverId => throw new System.NotImplementedException();

        public int MouseOverGuid => throw new System.NotImplementedException();
    }
}
