namespace CustomerSupport.Domain.Entities;

public interface IBaseEntity
{
    Guid Id { get; set; }
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
