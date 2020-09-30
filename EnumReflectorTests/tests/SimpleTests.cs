using NUnit.Framework;
using EnumReflector;
namespace EnumReflectorTests.Tests
{
    public class SimpleTests
    {
        [Test]
        public void Test_EnumName_TestEnum_8()
        {
            Assert.AreEqual(TestEnum_8.Value1.GetEnumName(), nameof(TestEnum_8.Value1));
        }
    }
}