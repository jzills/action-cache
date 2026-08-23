namespace Api.Database;

public class Account
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;  

    //public virtual ICollection<User> Users { get; set; }      
}