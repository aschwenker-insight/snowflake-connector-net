using System;

namespace Snowflake.Data.Tests.IntegrationTests
{
    using NUnit.Framework;
    using System.Data;
    using System.IO;
    using Core;
    using Client;
    using System.Threading.Tasks;
    
    [TestFixture]
    class SFReusableChunkTest : SFBaseTest
    {
        [Test]
        [Ignore("ReusableChunkTest")]
        public void ReusableChunkTestDone()
        {
            // Do nothing;
        }

        [Test]
        public void testDelCharPr431()
        {
            using (IDbConnection conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                IDbCommand cmd = conn.CreateCommand();
                int rowCount = 0;

                string createCommand = "create or replace table del_test (col string)";
                cmd.CommandText = createCommand;
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                int largeTableRowCount = 100000;
                string insertCommand = $"insert into del_test(select hex_decode_string(hex_encode('snow') || '7F' || hex_encode('FLAKE')) from table(generator(rowcount => {largeTableRowCount})))";
                cmd.CommandText = insertCommand;
                IDataReader insertReader = cmd.ExecuteReader();
                Assert.AreEqual(largeTableRowCount, insertReader.RecordsAffected);

                string selectCommand = "select * from del_test";
                cmd.CommandText = selectCommand;

                rowCount = 0;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var obj = new object[reader.FieldCount];
                        reader.GetValues(obj);
                        var val = obj[0] ?? System.String.Empty;
                        if (!val.ToString().Contains("u007f") && !val.ToString().Contains("\u007fu"))
                        {
                            rowCount++;
                        }
                    }
                }
                Assert.AreEqual(largeTableRowCount, rowCount);

                cmd.CommandText = "drop table if exists del_test";
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                // Reader's RecordsAffected should be available even if the connection is closed
                conn.Close();
            }
        }

        [Test]
        public void testParseJson()
        {
            IChunkParserFactory previous = ChunkParserFactory.Instance;
            ChunkParserFactory.Instance = new TestChunkParserFactory(1);
            
            using (IDbConnection conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                IDbCommand cmd = conn.CreateCommand();
                int rowCount = 0;

                string createCommand = "create or replace table car_sales(src variant)";
                cmd.CommandText = createCommand;
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                string insertCommand = @"
-- borrowed from https://docs.snowflake.com/en/user-guide/querying-semistructured.html#sample-data-used-in-examples
insert into car_sales (
select parse_json('{ 
    ""date"" : ""2017 - 04 - 28"", 
    ""dealership"" : ""Valley View Auto Sales"",
    ""salesperson"" : {
                    ""id"": ""55"",
      ""name"": ""Frank Beasley""
    },
    ""customer"" : [
      { ""name"": ""Joyce Ridgely"", ""phone"": ""16504378889"", ""address"": ""San Francisco, CA""}
    ],
    ""vehicle"" : [
       { ""make"": ""Honda"", ""model"": ""Civic"", ""year"": ""2017"", ""price"": ""20275"", ""extras"":[""ext warranty"", ""paint protection""]}
    ]
}') from table(generator(rowcount => 500))
)
";
                cmd.CommandText = insertCommand;
                IDataReader insertReader = cmd.ExecuteReader();
                Assert.AreEqual(500, insertReader.RecordsAffected);

                string selectCommand = "select * from car_sales";
                cmd.CommandText = selectCommand;
                cmd.CommandType = System.Data.CommandType.Text;

                rowCount = 0;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Newtonsoft.Json.JsonConvert.DeserializeObject(reader[0].ToString());
                        rowCount++;
                    }
                }
                Assert.AreEqual(500, rowCount);

                cmd.CommandText = "drop table if exists car_sales";
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                // Reader's RecordsAffected should be available even if the connection is closed
                conn.Close();
            }
            ChunkParserFactory.Instance = previous;
        }

        [Test]
        public void testChunkRetry()
        {
            IChunkParserFactory previous = ChunkParserFactory.Instance;
            ChunkParserFactory.Instance = new TestChunkParserFactory(6); // lower than default retry of 7

            using (IDbConnection conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                IDbCommand cmd = conn.CreateCommand();
                int rowCount = 0;

                string createCommand = "create or replace table del_test (col string)";
                cmd.CommandText = createCommand;
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                int largeTableRowCount = 100000;
                string insertCommand = $"insert into del_test(select hex_decode_string(hex_encode('snow') || '7F' || hex_encode('FLAKE')) from table(generator(rowcount => {largeTableRowCount})))";
                cmd.CommandText = insertCommand;
                IDataReader insertReader = cmd.ExecuteReader();
                Assert.AreEqual(largeTableRowCount, insertReader.RecordsAffected);

                string selectCommand = "select * from del_test";
                cmd.CommandText = selectCommand;

                rowCount = 0;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var obj = new object[reader.FieldCount];
                        reader.GetValues(obj);
                        var val = obj[0] ?? System.String.Empty;
                        if (!val.ToString().Contains("u007f") && !val.ToString().Contains("\u007fu"))
                        {
                            rowCount++;
                        }
                    }
                }
                Assert.AreEqual(largeTableRowCount, rowCount);

                cmd.CommandText = "drop table if exists del_test";
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                // Reader's RecordsAffected should be available even if the connection is closed
                conn.Close();
            }

            ChunkParserFactory.Instance = previous;
        }

        [Test]
        public void testExceptionThrownWhenChunkDownloadRetryCountExceeded()
        {            
            IChunkParserFactory previous = ChunkParserFactory.Instance;
            ChunkParserFactory.Instance = new TestChunkParserFactory(8); // larger than default max retry of 7
            using (IDbConnection conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                IDbCommand cmd = conn.CreateCommand();
                int rowCount = 0;

                string createCommand = "create or replace table del_test (col string)";
                cmd.CommandText = createCommand;
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                int largeTableRowCount = 100000;
                string insertCommand = $"insert into del_test(select hex_decode_string(hex_encode('snow') || '7F' || hex_encode('FLAKE')) from table(generator(rowcount => {largeTableRowCount})))";
                cmd.CommandText = insertCommand; 
                IDataReader insertReader = cmd.ExecuteReader();
                Assert.AreEqual(largeTableRowCount, insertReader.RecordsAffected);

                string selectCommand = "select * from del_test";
                cmd.CommandText = selectCommand;

                rowCount = 0;
                Assert.Throws<AggregateException>(() =>
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var obj = new object[reader.FieldCount];
                            reader.GetValues(obj);
                            var val = obj[0] ?? System.String.Empty;
                            if (!val.ToString().Contains("u007f") && !val.ToString().Contains("\u007fu"))
                            {
                                rowCount++;
                            }
                        }
                    }
                });
                Assert.AreNotEqual(largeTableRowCount, rowCount);

                cmd.CommandText = "drop table if exists del_test";
                rowCount = cmd.ExecuteNonQuery();
                Assert.AreEqual(0, rowCount);

                conn.Close();
            }
            ChunkParserFactory.Instance = previous;
        }

        class TestChunkParserFactory : IChunkParserFactory
        {
            private int _exceptionsThrown;
            private readonly int _expectedExceptionsNumber;
                
            public TestChunkParserFactory(int exceptionsToThrow)
            {
                _expectedExceptionsNumber = exceptionsToThrow;
                _exceptionsThrown = 0;
            }
            
            public IChunkParser GetParser(Stream stream)
            {
                if (++_exceptionsThrown <= _expectedExceptionsNumber)
                    return new ThrowingReusableChunkParser();

                return new ReusableChunkParser(stream);
            }
        }

        class ThrowingReusableChunkParser : IChunkParser
        {
            public Task ParseChunk(IResultChunk chunk)
            { 
                throw new Exception("json parsing error.");
            }
        }
    }
}