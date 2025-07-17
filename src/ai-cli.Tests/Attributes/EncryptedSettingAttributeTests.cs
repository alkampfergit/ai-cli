using AiCli.Attributes;
using FluentAssertions;
using System.Reflection;

namespace AiCli.Tests.Attributes;

public class EncryptedSettingAttributeTests
{
    [Fact]
    public void EncryptedSettingAttribute_ShouldBeApplicableToProperties()
    {
        // Act
        var attribute = new EncryptedSettingAttribute();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeOfType<EncryptedSettingAttribute>();
    }

    [Fact]
    public void EncryptedSettingAttribute_ShouldHaveCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(EncryptedSettingAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Property);
        attributeUsage.AllowMultiple.Should().BeFalse();
    }

    private class TestClass
    {
        [EncryptedSetting]
        public string? EncryptedProperty { get; set; }

        public string? NormalProperty { get; set; }
    }

    [Fact]
    public void EncryptedSettingAttribute_ShouldBeDetectableViaReflection()
    {
        // Arrange
        var testType = typeof(TestClass);
        var encryptedProperty = testType.GetProperty(nameof(TestClass.EncryptedProperty));
        var normalProperty = testType.GetProperty(nameof(TestClass.NormalProperty));

        // Act
        var encryptedAttribute = encryptedProperty?.GetCustomAttribute<EncryptedSettingAttribute>();
        var normalAttribute = normalProperty?.GetCustomAttribute<EncryptedSettingAttribute>();

        // Assert
        encryptedAttribute.Should().NotBeNull();
        normalAttribute.Should().BeNull();
    }
}