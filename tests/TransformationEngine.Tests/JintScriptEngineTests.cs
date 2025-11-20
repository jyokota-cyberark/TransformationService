using Xunit;
using TransformationEngine.ScriptEngines;

namespace TransformationEngine.Tests;

public class JintScriptEngineTests
{
    [Fact]
    public void Execute_SimpleTransformation_ReturnsModifiedData()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = @"
            return {
                name: data.name.toUpperCase(),
                email: data.email.toLowerCase(),
                age: data.age + 1
            };
        ";
        var inputData = new Dictionary<string, object?>
        {
            ["name"] = "John Doe",
            ["email"] = "JOHN@EXAMPLE.COM",
            ["age"] = 30
        };

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var outputData = result.Data as Dictionary<string, object?>;
        Assert.NotNull(outputData);
        Assert.Equal("JOHN DOE", outputData["name"]);
        Assert.Equal("john@example.com", outputData["email"]);
        Assert.Equal(31, Convert.ToInt32(outputData["age"]));
    }

    [Fact]
    public void Execute_WithHelperFunctions_UsesBuiltInHelpers()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = @"
            return {
                trimmed: trim('  hello  '),
                upper: upperCase('test'),
                lower: lowerCase('TEST')
            };
        ";
        var inputData = new Dictionary<string, object?>();

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var outputData = result.Data as Dictionary<string, object?>;
        Assert.NotNull(outputData);
        Assert.Equal("hello", outputData["trimmed"]);
        Assert.Equal("TEST", outputData["upper"]);
        Assert.Equal("test", outputData["lower"]);
    }

    [Fact]
    public void Execute_WithArrayManipulation_HandlesArrays()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = @"
            return {
                original: data.items,
                filtered: data.items.filter(x => x > 5),
                mapped: data.items.map(x => x * 2),
                count: data.items.length
            };
        ";
        var inputData = new Dictionary<string, object?>
        {
            ["items"] = new List<int> { 1, 3, 5, 7, 9 }
        };

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var outputData = result.Data as Dictionary<string, object?>;
        Assert.NotNull(outputData);
        Assert.Equal(5, Convert.ToInt32(outputData["count"]));
    }

    [Fact]
    public void Execute_WithConditionalLogic_AppliesConditions()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = @"
            return {
                status: data.age >= 18 ? 'adult' : 'minor',
                discount: data.age >= 65 ? 0.2 : 0.0,
                category: data.age < 13 ? 'child' : data.age < 20 ? 'teen' : 'adult'
            };
        ";
        var inputData = new Dictionary<string, object?>
        {
            ["age"] = 25
        };

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var outputData = result.Data as Dictionary<string, object?>;
        Assert.NotNull(outputData);
        Assert.Equal("adult", outputData["status"]);
        Assert.Equal(0.0, Convert.ToDouble(outputData["discount"]));
        Assert.Equal("adult", outputData["category"]);
    }

    [Fact]
    public void Execute_WithNestedObjects_HandlesNesting()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = @"
            return {
                fullName: data.user.firstName + ' ' + data.user.lastName,
                city: data.user.address.city,
                hasEmail: !!data.user.contact.email
            };
        ";
        var inputData = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?>
            {
                ["firstName"] = "Jane",
                ["lastName"] = "Smith",
                ["address"] = new Dictionary<string, object?>
                {
                    ["city"] = "New York",
                    ["state"] = "NY"
                },
                ["contact"] = new Dictionary<string, object?>
                {
                    ["email"] = "jane@example.com",
                    ["phone"] = "555-1234"
                }
            }
        };

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var outputData = result.Data as Dictionary<string, object?>;
        Assert.NotNull(outputData);
        Assert.Equal("Jane Smith", outputData["fullName"]);
        Assert.Equal("New York", outputData["city"]);
        Assert.True(Convert.ToBoolean(outputData["hasEmail"]));
    }

    [Fact]
    public void Execute_WithInvalidScript_ReturnsError()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = "this is invalid javascript syntax {{{";
        var inputData = new Dictionary<string, object?>();

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Execute_WithNullOrUndefined_HandlesGracefully()
    {
        // Arrange
        var engine = new JintScriptEngine(null);
        var script = @"
            return {
                hasName: data.name !== undefined && data.name !== null,
                nameOrDefault: data.name || 'Unknown',
                safeAccess: data.user?.profile?.bio || 'No bio'
            };
        ";
        var inputData = new Dictionary<string, object?>
        {
            ["name"] = null
        };

        // Act
        var result = engine.Execute(script, inputData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var outputData = result.Data as Dictionary<string, object?>;
        Assert.NotNull(outputData);
        Assert.False(Convert.ToBoolean(outputData["hasName"]));
        Assert.Equal("Unknown", outputData["nameOrDefault"]);
        Assert.Equal("No bio", outputData["safeAccess"]);
    }
}
