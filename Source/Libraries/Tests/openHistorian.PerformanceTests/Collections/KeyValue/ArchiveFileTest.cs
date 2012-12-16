﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NUnit.Framework;
using openHistorian.IO.Unmanaged;
using openHistorian.Archive;

namespace openHistorian.Collections.KeyValue
{
    [TestFixture]
    public class ArchiveFileTest
    {

        [Test]
        public static unsafe void ReadFiles()
        {
            string path1 = "c:\\temp\\ArchiveFileTest1.d2";
            string path2 = "c:\\temp\\ArchiveFileTest2.d2";
            string path3 = "c:\\temp\\ArchiveFileTest3.d2";

            using (ArchiveFile file1 = ArchiveFile.OpenFile(path1, AccessMode.ReadOnly))
            using (ArchiveFile file2 = ArchiveFile.OpenFile(path2, AccessMode.ReadOnly))
            using (ArchiveFile file3 = ArchiveFile.OpenFile(path3, AccessMode.ReadOnly))
            {

                var scan1 = file1.CreateSnapshot().OpenInstance().GetDataRange();
                var scan2 = file2.CreateSnapshot().OpenInstance().GetDataRange();
                var scan3 = file3.CreateSnapshot().OpenInstance().GetDataRange();
                scan1.SeekToKey(0, 0);
                scan2.SeekToKey(0, 0);
                scan3.SeekToKey(0, 0);
                ulong key1, key2, value1, value2;
                ulong key11, key22, value11, value22;
                while (scan1.GetNextKey(out key1, out key2, out value1, out value2))
                {
                    Assert.IsTrue(scan2.GetNextKey(out key11, out key22, out value11, out value22));
                    Assert.AreEqual(key1, key11);
                    Assert.AreEqual(key2, key22);
                    Assert.AreEqual(value1, value11);
                    Assert.AreEqual(value2, value22);
                    Assert.IsTrue(scan3.GetNextKey(out key11, out key22, out value11, out value22));
                    Assert.AreEqual(key1, key11);
                    Assert.AreEqual(key2, key22);
                    Assert.AreEqual(value1, value11);
                    Assert.AreEqual(value2, value22);
                }
                Assert.IsFalse(scan2.GetNextKey(out key1, out key2, out value1, out value2));
                Assert.IsFalse(scan3.GetNextKey(out key1, out key2, out value1, out value2));

            }
        }

        [Test]
        public static unsafe void RunBenchmark()
        {
            string path1 = "c:\\temp\\ArchiveFileTest1.d2";
            string path2 = "c:\\temp\\ArchiveFileTest2.d2";
            string path3 = "c:\\temp\\ArchiveFileTest3.d2";

            var bs0 = new BinaryStream();
            var tree0 = new SortedTree256(bs0, 4096);

            if (File.Exists(path1))
                File.Delete(path1);
            if (File.Exists(path2))
                File.Delete(path2);
            if (File.Exists(path3))
                File.Delete(path3);

            using (ArchiveFile file1 = ArchiveFile.CreateFile(path1, CompressionMethod.None))
            using (ArchiveFile file2 = ArchiveFile.CreateFile(path2, CompressionMethod.DeltaEncoded))
            using (ArchiveFile file3 = ArchiveFile.CreateFile(path3, CompressionMethod.TimeSeriesEncoded))
            using (var edit1 = file1.BeginEdit())
            using (var edit2 = file2.BeginEdit())
            using (var edit3 = file3.BeginEdit())
            {
                var hist = new OldHistorianReader("C:\\Unison\\GPA\\ArchiveFiles\\archive1_archive_2012-07-26 15!35!36.166_to_2012-07-26 15!40!36.666.d");
                //var hist = new OldHistorianReader(@"D:\Projects\Applications\openPDC\Synchrophasor\Current Version\Build\Output\Debug\Applications\openPDC\Archive\ppa_archive_2012-11-06 16!00!51.233_to_2012-11-06 16!07!16.933.d");
                Action<OldHistorianReader.Points> del = (x) =>
                    {
                        tree0.Add((ulong)x.Time.Ticks, (ulong)x.PointID, x.flags, *(uint*)&x.Value);
                    };
                hist.Read(del);

                //tree0 = SortPoints(tree0);

                var scan0 = tree0.GetDataRange();
                scan0.SeekToKey(0, 0);
                ulong key1, key2, value1, value2;
                while (scan0.GetNextKey(out key1, out key2, out value1, out value2))
                {
                    edit1.AddPoint(key1, key2, value1, value2);
                    edit2.AddPoint(key1, key2, value1, value2);
                    edit3.AddPoint(key1, key2, value1, value2);
                }

                edit1.Commit();
                edit2.Commit();
                edit3.Commit();
            }
        }



        public static SortedTree256 SortPoints(SortedTree256 tree)
        {
            ulong maxPointId = 0;
            var scan = tree.GetDataRange();
            ulong key1, key2, value1, value2;
            scan.SeekToKey(0, 0);
            while (scan.GetNextKey(out key1, out key2, out value1, out value2))
            {
                maxPointId = Math.Max(key2, maxPointId);
            }

            var map = new PointValue[(int)maxPointId + 1];

            scan.SeekToKey(0, 0);
            while (scan.GetNextKey(out key1, out key2, out value1, out value2))
            {
                if (map[(int)key2] == null)
                    map[(int)key2] = new PointValue();
                map[(int)key2].Value = value2;
            }

            var list = new List<PointValue>();
            foreach (var pv in map)
            {
                if (pv != null)
                    list.Add(pv);
            }
            list.Sort();

            for (uint x = 0; x < list.Count; x++)
            {
                list[(int)x].NewPointId = x;
            }

            var tree2 = new SortedTree256(new BinaryStream(), 4096);
            scan.SeekToKey(0, 0);
            while (scan.GetNextKey(out key1, out key2, out value1, out value2))
            {
                tree2.Add(key1, map[(int)key2].NewPointId, value1, value2);
            }

            return tree2;
        }

        class PointValue : IComparable<PointValue>
        {
            public ulong NewPointId;
            public ulong Value;

            /// <summary>
            /// Compares the current object with another object of the same type.
            /// </summary>
            /// <returns>
            /// A value that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> parameter.Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>. 
            /// </returns>
            /// <param name="other">An object to compare with this object.</param>
            public int CompareTo(PointValue other)
            {
                return Value.CompareTo(other.Value);
            }
        }

    }
}
