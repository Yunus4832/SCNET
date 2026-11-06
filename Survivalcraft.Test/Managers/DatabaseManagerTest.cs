using System.Reflection;

using EntitySystem.TemplatesDatabase;

using Game.Managers;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class DatabaseManagerTest
{
    [Fact]
    public void EntityOverridesDoNotChangeOtherEntitiesOrTheCachedTemplate()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var definition = new DatabaseObject(project.GameDatabase.EntityTemplateType, "OverrideIsolationTest");
        var parametersDefinition = new DatabaseObject(
            project.GameDatabase.Database.FindDatabaseObjectType("ParameterSet", true)!,
            "Parameters"
        );
        var parameters = new ValuesDictionary { DatabaseObject = parametersDefinition };
        parameters.SetValue("First", 1);
        parameters.SetValue("Second", 2);
        var template = new ValuesDictionary { DatabaseObject = definition };
        template.SetValue("Parameters", parameters);
        var cache = (Dictionary<string, ValuesDictionary>)typeof(DatabaseManager)
            .GetField("_valueDictionaries", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        cache.Add(definition.Name, template);
        try
        {
            var firstOverrides = new ValuesDictionary();
            var firstParameters = new ValuesDictionary();
            firstParameters.SetValue("First", 10);
            firstOverrides.SetValue("Parameters", firstParameters);
            using var first = DatabaseManager.CreateEntity(project, definition.Name, firstOverrides, true)!;

            var secondOverrides = new ValuesDictionary();
            var secondParameters = new ValuesDictionary();
            secondParameters.SetValue("Second", 20);
            secondOverrides.SetValue("Parameters", secondParameters);
            using var second = DatabaseManager.CreateEntity(project, definition.Name, secondOverrides, true)!;

            var firstValues = first.ValuesDictionary.GetValue<ValuesDictionary>("Parameters");
            var secondValues = second.ValuesDictionary.GetValue<ValuesDictionary>("Parameters");
            Assert.Equal(10, firstValues.GetValue<int>("First"));
            Assert.Equal(2, firstValues.GetValue<int>("Second"));
            Assert.Equal(1, secondValues.GetValue<int>("First"));
            Assert.Equal(20, secondValues.GetValue<int>("Second"));
            Assert.Same(template, DatabaseManager.FindEntityValuesDictionary(definition.Name, true));
            Assert.Same(parameters, template.GetValue<ValuesDictionary>("Parameters"));
            Assert.Equal(1, parameters.GetValue<int>("First"));
            Assert.Equal(2, parameters.GetValue<int>("Second"));
            Assert.NotSame(parameters, firstValues);
            Assert.NotSame(firstValues, secondValues);
            Assert.Same(definition, first.ValuesDictionary.DatabaseObject);
            Assert.Same(parametersDefinition, firstValues.DatabaseObject);
            Assert.Same(parametersDefinition, secondValues.DatabaseObject);
        }
        finally
        {
            cache.Remove(definition.Name);
        }
    }
}
