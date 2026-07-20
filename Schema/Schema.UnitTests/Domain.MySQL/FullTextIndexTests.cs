// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;
using Schema.Domain.MySQL;

namespace Schema.UnitTests.Domain.MySQL
{
    [TestFixture]
    public class MySqlFullTextIndexTests
    {
        [Test]
        public void InheritsFromDynamicBase()
        {
            Assert.That(new FullTextIndex(), Is.InstanceOf<DynamicBase>());
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var fti = new FullTextIndex();

            Assert.That(fti.Name, Is.Null);
            Assert.That(fti.Columns, Is.Null);
            Assert.That(fti.Parser, Is.Null);
            Assert.That(fti.Comment, Is.Null);
            Assert.That(fti.ShouldApplyExpression, Is.Null);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllProperties()
        {
            var fti = new FullTextIndex
            {
                Name = "ft_product_search",
                Columns = "name, description",
                Parser = "ngram",
                Comment = "Product full-text search",
                ShouldApplyExpression = "SELECT 1"
            };

            var json = JsonConvert.SerializeObject(fti);
            var deserialized = JsonConvert.DeserializeObject<FullTextIndex>(json);

            Assert.That(deserialized.Name, Is.EqualTo("ft_product_search"));
            Assert.That(deserialized.Columns, Is.EqualTo("name, description"));
            Assert.That(deserialized.Parser, Is.EqualTo("ngram"));
            Assert.That(deserialized.Comment, Is.EqualTo("Product full-text search"));
            Assert.That(deserialized.ShouldApplyExpression, Is.EqualTo("SELECT 1"));
        }

        [Test]
        public void JsonRoundTrip_PreservesVariantName()
        {
            var obj = new FullTextIndex { Name = "ft_test", Columns = "col1", ShouldApplyExpression = "1=1", VariantName = "EU region" };
            var json = JsonConvert.SerializeObject(obj);
            var deserialized = JsonConvert.DeserializeObject<FullTextIndex>(json);
            Assert.That(deserialized.VariantName, Is.EqualTo("EU region"));
        }

        [Test]
        public void DifferentFromSqlServerFullTextIndex()
        {
            // MySQL FullTextIndex has Name, Columns, Parser, Comment
            // SQL Server FullTextIndex has FullTextCatalog, KeyIndex, ChangeTracking, StopList, Columns
            var mysqlFti = new FullTextIndex();
            var sqlFti = new Schema.Domain.SqlServer.FullTextIndex();

            Assert.That(mysqlFti.GetType().Namespace, Is.EqualTo("Schema.Domain.MySQL"));
            Assert.That(sqlFti.GetType().Namespace, Is.EqualTo("Schema.Domain.SqlServer"));
        }
    }
}
