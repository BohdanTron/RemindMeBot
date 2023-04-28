﻿using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Testing.XUnit;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.Core;
using RemindMeBot.Resources;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Common
{
    public abstract class BotTestBase
    {
        protected List<IMiddleware> Middlewares;

        protected IStringLocalizer<BotMessages> Localizer = Substitute.For<IStringLocalizer<BotMessages>>();

        protected BotTestBase(ITestOutputHelper output)
        {
            Middlewares = new List<IMiddleware> { new XUnitDialogTestLogger(output) };
            SetupLocalizer();
        }

        protected static void SetCurrentCulture(string culture)
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
        }

        private void SetupLocalizer()
        {
            var resourceManager = new ResourceManager(
                $"{typeof(BotMessages).Assembly.GetName().Name}.Resources.BotMessages", typeof(BotMessages).Assembly);

            Localizer[Arg.Any<string>()].Returns(LocalizedString);
            Localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(LocalizedString);

            LocalizedString? LocalizedString(CallInfo info)
            {
                var key = (string) info[0];
                var value = resourceManager.GetString(key, CultureInfo.CurrentCulture);

                return value is null ? null : new LocalizedString(key, value);
            }
        }

    }
}
