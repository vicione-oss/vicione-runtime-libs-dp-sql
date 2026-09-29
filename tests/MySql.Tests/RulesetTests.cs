using AwesomeAssertions;
using ViciOne.Tree.Builder.Rules.Yaml;
using ViciOne.Tree.Builder.Validation;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class Ruleset_
{
    [Fact]
    public void MySQL_is_valid()
    {
        var ruleset = RulesDeserializer.Deserialize("MySql.yaml");

        var result = ValidationManager.Validate(ruleset);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void MariaDB_is_valid()
    {
        var ruleset = RulesDeserializer.Deserialize("MariaDb.yaml");

        var result = ValidationManager.Validate(ruleset);

        result.IsValid.Should().BeTrue();
        result.Messages.Should().BeEmpty();
    }
}
