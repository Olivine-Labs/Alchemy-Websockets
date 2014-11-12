using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Alchemy.Handlers.WebSocket.rfc6455
{
    [TestFixture]
    public class DataFrameTest
    {
        #region Setup/Teardown

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

        #endregion

        private DataFrame _dataframe;

        public void TestBackAndForth()
        {
            List<ArraySegment<byte>> tempList = _dataframe.AsRaw();
            List<ArraySegment<byte>> originalList =
                tempList.Select(item => new ArraySegment<byte>((byte[]) item.Array.Clone())).ToList();
            _dataframe.AsFrame();

            List<ArraySegment<byte>> list = _dataframe.AsRaw();

            Assert.AreEqual(list.Count, originalList.Count);
            for (int index = 0; index < list.Count; index++)
            {
                for (int index2 = 0; index2 < list[index].Array.Length; index2++)
                {
                    Assert.AreEqual(((originalList[index])).Array[index2], list[index].Array[index2]);
                }
            }
        }

        public static byte[] GenerateRandomArray(int nbOfRows)
        {
            const int minNumber = 0x0;
            const int maxNumber = 0xff;
            var randomNumber = new Random();
            var newarray = new byte[nbOfRows];
            for (int row = 0; row < nbOfRows; row++)
            {
                newarray[row] = (byte) randomNumber.Next(minNumber, maxNumber);
            }
            return newarray;
        }

        [Test]
        public void BackAndForthBig()
        {
            int dataSize = 10000000;
            while (dataSize > 0)
            {
                byte[] testData = GenerateRandomArray(512);
                _dataframe.Append(testData);
                dataSize -= 512;
            }

            TestBackAndForth();
        }

        [Test]
        public void BackAndForthMedium()
        {
            byte[] testData = GenerateRandomArray(10000);
            _dataframe.Append(testData);
            TestBackAndForth();
        }

        [Test]
        public void BackAndForthSmall()
        {
            byte[] testData = GenerateRandomArray(50);
            _dataframe.Append(testData);
            TestBackAndForth();
        }
    }
}