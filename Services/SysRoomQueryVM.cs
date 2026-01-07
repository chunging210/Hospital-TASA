namespace TASA.Services;

public class SysRoomQueryVM : BaseQueryVM
{
    public string? Building { get; set; } = string.Empty;  
    
    public string? Floor { get; set; } = string.Empty; 

    public Guid? DepartmentId { get; set; }
}