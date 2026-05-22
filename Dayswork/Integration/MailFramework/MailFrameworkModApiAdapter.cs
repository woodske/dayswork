using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using StardewValley;

namespace Dayswork.Integration.MailFramework;

// Adapter for Mail Framework Mod 1.20.0 (DIGUS.MailFrameworkMod).
//
// MFM's real API is not the simple RegisterLetter(id, text, items) shape initially guessed
// during U-14 code generation. It exposes RegisterLetter(ILetter, condition, callback,
// dynamicItems) and concrete public Letter/ApiLetter types. Reflection keeps Dayswork from
// taking a compile-time dependency on another mod assembly while still calling the confirmed
// runtime API exactly.
internal sealed class MailFrameworkModApiAdapter
{
    private const string LetterTypeName = "MailFrameworkMod.Letter";
    private const string ApiLetterTypeName = "MailFrameworkMod.Api.ApiLetter";
    private const string ApiLetterInterfaceTypeName = "MailFrameworkMod.Api.ILetter";
    private const string MailRepositoryTypeName = "MailFrameworkMod.MailRepository";

    private readonly object _api;
    private readonly Type _apiLetterInterfaceType;
    private readonly ConstructorInfo _letterTextOnlyCtor;
    private readonly ConstructorInfo _apiLetterCtor;
    private readonly FieldInfo _titleField;
    private readonly MethodInfo _registerLetter;
    private readonly MethodInfo _findLetter;
    private readonly MethodInfo _removeLetter;

    internal MailFrameworkModApiAdapter(object api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));

        var assembly = api.GetType().Assembly;
        var letterType = RequireType(assembly, LetterTypeName);
        var apiLetterType = RequireType(assembly, ApiLetterTypeName);
        var mailRepositoryType = RequireType(assembly, MailRepositoryTypeName);
        _apiLetterInterfaceType = RequireType(assembly, ApiLetterInterfaceTypeName);

        _letterTextOnlyCtor = letterType.GetConstructors()
            .SingleOrDefault(c =>
            {
                var p = c.GetParameters();
                return p.Length == 5
                    && p[0].ParameterType == typeof(string)
                    && p[1].ParameterType == typeof(string);
            })
            ?? throw MissingMember(LetterTypeName, "text-only constructor");

        _apiLetterCtor = apiLetterType.GetConstructors()
            .SingleOrDefault(c =>
            {
                var p = c.GetParameters();
                return p.Length == 1 && p[0].ParameterType == letterType;
            })
            ?? throw MissingMember(ApiLetterTypeName, "Letter constructor");

        _titleField = letterType.GetField("Title", BindingFlags.Public | BindingFlags.Instance)
            ?? throw MissingMember(LetterTypeName, "Title field");

        _registerLetter = api.GetType().GetMethods()
            .SingleOrDefault(m =>
            {
                var p = m.GetParameters();
                return m.Name == "RegisterLetter"
                    && p.Length == 4
                    && p[0].ParameterType == _apiLetterInterfaceType;
            })
            ?? throw MissingMember(api.GetType().FullName ?? "MailFrameworkModApi", "RegisterLetter");

        _findLetter = mailRepositoryType.GetMethod("FindLetter", BindingFlags.Public | BindingFlags.Static)
            ?? throw MissingMember(MailRepositoryTypeName, "FindLetter");
        _removeLetter = mailRepositoryType.GetMethod("RemoveLetter", BindingFlags.Public | BindingFlags.Static)
            ?? throw MissingMember(MailRepositoryTypeName, "RemoveLetter");
    }

    internal void RegisterLetter(string id, string title, string text, List<Item> attachments, int earliestDeliveryDay, int moneyReward = 0)
    {
        var letter = _letterTextOnlyCtor.Invoke(new object?[] { id, text, null, null, 0 });
        _titleField.SetValue(letter, title);

        var apiLetter = _apiLetterCtor.Invoke(new[] { letter });
        var condition = CreateDeliveryCondition(id, earliestDeliveryDay);
        var callback = CreateRemoveAfterReadCallback(id, moneyReward);
        var dynamicItems = attachments.Count > 0
            ? CreateDynamicItemsFactory(attachments)
            : null;
        _registerLetter.Invoke(_api, new object?[] { apiLetter, condition, callback, dynamicItems });
    }

    private Delegate CreateDeliveryCondition(string id, int earliestDeliveryDay)
    {
        var conditionType = _registerLetter.GetParameters()[1].ParameterType;
        var letterParameter = Expression.Parameter(_apiLetterInterfaceType, "letter");
        var method = typeof(MailFrameworkModApiAdapter).GetMethod(
            nameof(IsReadyToDeliver),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var body = Expression.Call(method, Expression.Constant(id), Expression.Constant(earliestDeliveryDay));
        return Expression.Lambda(conditionType, body, letterParameter).Compile();
    }

    private Delegate CreateRemoveAfterReadCallback(string id, int moneyReward)
    {
        var callbackType = _registerLetter.GetParameters()[2].ParameterType;
        var letterParameter = Expression.Parameter(_apiLetterInterfaceType, "letter");
        var method = typeof(MailFrameworkModApiAdapter).GetMethod(
            nameof(OnLetterRead),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var body = Expression.Call(
            Expression.Constant(this), method,
            Expression.Constant(id), Expression.Constant(moneyReward));
        return Expression.Lambda(callbackType, body, letterParameter).Compile();
    }

    private Delegate CreateDynamicItemsFactory(List<Item> attachments)
    {
        var dynamicItemsType = _registerLetter.GetParameters()[3].ParameterType;
        var letterParameter = Expression.Parameter(_apiLetterInterfaceType, "letter");
        var method = typeof(MailFrameworkModApiAdapter).GetMethod(
            nameof(CloneAttachments),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var body = Expression.Call(method, Expression.Constant(attachments));
        return Expression.Lambda(dynamicItemsType, body, letterParameter).Compile();
    }

    private static bool IsReadyToDeliver(string id, int earliestDeliveryDay)
    {
        if (Game1.player is null || Game1.Date.TotalDays < earliestDeliveryDay)
            return false;

        // MFM's API letters remain in its repository until removed. This extra guard makes the
        // letter one-shot even before the callback has pruned the repository.
        return !Game1.player.mailReceived.Contains(id);
    }

    // Runs when the player reads/closes the letter. Removes it from MFM's repository (one-shot) and,
    // if the letter carries a refund, credits the gold on collection — the approved fallback for
    // mailed refunds when MFM has no native money attachment (Clar-3=A / BR-REF-04).
    private void OnLetterRead(string id, int moneyReward)
    {
        var letter = _findLetter.Invoke(null, new object[] { id });
        if (letter is not null)
            _removeLetter.Invoke(null, new[] { letter });

        if (moneyReward > 0 && Game1.player is not null)
            Game1.player.Money += moneyReward;
    }

    private static List<Item> CloneAttachments(List<Item> attachments)
    {
        var result = new List<Item>(attachments.Count);
        foreach (var item in attachments)
        {
            var copy = item.getOne();
            copy.Stack = item.Stack;
            result.Add(copy);
        }

        return result;
    }

    private static Type RequireType(Assembly assembly, string name) =>
        assembly.GetType(name) ?? throw MissingMember(assembly.FullName ?? "MailFrameworkMod", name);

    private static MissingMemberException MissingMember(string typeName, string memberName) =>
        new(typeName, memberName);
}
