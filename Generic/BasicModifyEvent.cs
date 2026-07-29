using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Generic;

[EventBoot(FuncType.modify, KeepAll = true)]
[Description("修改数据")]
public class BasicModifyEvent : DomainEvent
{
}