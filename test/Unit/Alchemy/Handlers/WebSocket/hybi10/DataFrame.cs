using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Alchemy.Handlers.WebSocket.hybi10
{
    [TestFixture]
    public class DataFrameTest
    {
        private Alchemy.Handlers.WebSocket.hybi10.DataFrame _dataframe = null;
        [SetUp]
        public void SetUp()
        {
            _dataframe = new DataFrame();
        }

        [TearDown]
        public void TearDown()
        {
            _dataframe = null;
        }

        [Test]
        public void BackAndForthSmall()
        {
            byte[] testData = new byte[50];
            _dataframe.Append(testData);

            _dataframe.AsFrame();

            List<ArraySegment<byte>> list = _dataframe.AsRaw();

            var temp = new byte[0];
            foreach(var someData in list)
            {
                int len = temp.Length;
                var temp2 = new byte[someData.Array.Length + len];
                Array.Copy(someData.Array, 0, temp2, len, someData.Array.Length - len);
                Array.Copy(temp, 0, temp2, 0, len);
                temp = temp2;
            }
            Assert.AreEqual(temp.Length, testData.Length);
            for(int index = 0; index < temp.Length; index++)
            {
                Assert.AreEqual(testData[index], temp[index]);
            }
        }

        [Test]
        public void BackAndForthMedium()
        {

        }

        [Test]
        public void BackAndForthBig()
        {

        }
    }
}
