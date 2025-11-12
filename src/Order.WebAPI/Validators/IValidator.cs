namespace Order.WebAPI.Validators;

public interface IValidator<T>
{
    void ValidateAndThrow(T entity);
}