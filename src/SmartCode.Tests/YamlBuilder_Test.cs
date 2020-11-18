using SmartCode.Configuration;
using SmartCode.Configuration.ConfigBuilders;
using System;
using Xunit;

namespace SmartCode.Tests
{
    public class YamlBuilder_Test
    {
        [Fact]
        public void Build()
        {
            var configPath = @"E:\projects\SmartCode\doc\SmartCode.yml";
            YamlBuilder yamlBuilder = new YamlBuilder(configPath);
            var project = yamlBuilder.Build();
        }
    }
}
