using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Phcis.Booking.Client.Validation;

/// <summary>
/// Plugs a FluentValidation validator into an EditForm, the same way DataAnnotationsValidator does
/// for attributes. The validators are the shared ones the API also runs, so the rules are written once.
/// Drop it inside an EditForm: <c>&lt;FluentValidator TModel="LoginRequest" /&gt;</c>.
/// </summary>
public sealed class FluentValidator<TModel> : ComponentBase, IDisposable
{
    private ValidationMessageStore? _messages;
    private EditContext? _subscribedContext;

    [CascadingParameter] private EditContext? EditContext { get; set; }

    [Inject] private IValidator<TModel> Validator { get; set; } = null!;

    protected override void OnParametersSet()
    {
        if (EditContext is null)
            throw new InvalidOperationException($"{nameof(FluentValidator<TModel>)} must be placed inside an EditForm.");
        if (ReferenceEquals(EditContext, _subscribedContext)) return;

        Unsubscribe();
        _subscribedContext = EditContext;
        _messages = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += OnValidationRequested;
        EditContext.OnFieldChanged += OnFieldChanged;
    }

    /// <summary>Shows errors returned by the API (e.g. "email already registered") next to the right fields.</summary>
    public void DisplayErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        if (_subscribedContext is null || _messages is null) return;

        foreach (var (property, messages) in errors)
        {
            _messages.Add(_subscribedContext.Field(property), messages);
        }
        _subscribedContext.NotifyValidationStateChanged();
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _messages!.Clear();
        foreach (var error in Validator.Validate(Model).Errors)
        {
            _messages.Add(_subscribedContext!.Field(error.PropertyName), error.ErrorMessage);
        }
        _subscribedContext!.NotifyValidationStateChanged();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        var field = e.FieldIdentifier;
        var result = Validator.Validate(Model, options => options.IncludeProperties(field.FieldName));

        _messages!.Clear(field);
        foreach (var error in result.Errors.Where(error => error.PropertyName == field.FieldName))
        {
            _messages.Add(field, error.ErrorMessage);
        }
        _subscribedContext!.NotifyValidationStateChanged();
    }

    private TModel Model => (TModel)_subscribedContext!.Model;

    private void Unsubscribe()
    {
        if (_subscribedContext is null) return;
        _subscribedContext.OnValidationRequested -= OnValidationRequested;
        _subscribedContext.OnFieldChanged -= OnFieldChanged;
    }

    public void Dispose() => Unsubscribe();
}
