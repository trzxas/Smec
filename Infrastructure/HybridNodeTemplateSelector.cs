using Smec.CustomControls;
using Smec.Models;
using Smec.ViewModels;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using Application = System.Windows.Application;

namespace Smec.Infrastructure
{

    public enum TemplateTargetMode
    {
        FullNode, // Подменяет весь шаблон компонента (используется в ComponentsLayer / StockNodeIcon)
        BodyOnly  // Подменяет только внутренность корпуса внутри NodeTemplate (пины и подписи остаются стандартными)
    }

    public class HybridNodeTemplateSelector : DataTemplateSelector
    {
        private static readonly Dictionary<string, DataTemplate> _xamlCache = [];

        // Режим работы селектора (задаётся в XAML при объявлении ресурса)
        public TemplateTargetMode TargetMode { get; set; } = TemplateTargetMode.FullNode;

        // Ключ шаблона по умолчанию (для FullNode — "NodeTemplate", для BodyOnly — "DefaultBodyTemplate")
        public string FallbackTemplateKey { get; set; } = "NodeTemplate";

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is NodeVm vm && container is FrameworkElement element)
            {
                // В зависимости от режима берём нужное свойство из модели Node
                object? requiredTemplate = TargetMode == TemplateTargetMode.FullNode
                    ? vm.Model.CustomTemplate
                    : vm.Model.CustomBodyTemplate;

                return SelectTemplateProc(vm.Model, element, requiredTemplate, FallbackTemplateKey);
            }

            return base.SelectTemplate(item, container);
        }

        protected DataTemplate? SelectTemplateProc(
            Node item,
            FrameworkElement element,
            object? requiredTemplate,
            string fallbackTemplateKey)
        {
            if (requiredTemplate is DataTemplate customTemplate)
                return customTemplate;

            if (requiredTemplate is string textStr && !string.IsNullOrWhiteSpace(textStr))
            {
                string trimmedStr = textStr.Trim();

                if (trimmedStr.StartsWith('<'))
                {
                    if (_xamlCache.TryGetValue(trimmedStr, out var cached))
                        return cached;

                    string xamlToParse = trimmedStr.StartsWith("<DataTemplate")
                        ? trimmedStr
                        : $"<DataTemplate>{trimmedStr}</DataTemplate>";

                    try
                    {
                        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xamlToParse));
                        var context = new ParserContext();
                        context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                        context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

                        Type controlType = typeof(CustomControl);
                        context.XmlnsDictionary.Add("cc", $"clr-namespace:{controlType.Namespace};assembly={controlType.Assembly.GetName().Name}");

                        if (XamlReader.Load(stream, context) is DataTemplate parsedTemplate)
                        {
                            _xamlCache[trimmedStr] = parsedTemplate;
                            return parsedTemplate;
                        }
                    }
                    catch (XamlParseException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка парсинга XAML в Node ({item.GetType().Name}): {ex.Message}");
                    }
                }
                else
                {
                    if (element.TryFindResource(trimmedStr) is DataTemplate localTemplate)
                        return localTemplate;

                    if (Application.Current?.TryFindResource(trimmedStr) is DataTemplate globalTemplate)
                        return globalTemplate;
                }
            }

            if (!string.IsNullOrEmpty(fallbackTemplateKey))
            {
                if (element.TryFindResource(fallbackTemplateKey) is DataTemplate fallback)
                    return fallback;

                if (Application.Current?.TryFindResource(fallbackTemplateKey) is DataTemplate globalFallback)
                    return globalFallback;
            }

            return base.SelectTemplate(item, element);
        }
    }


}

