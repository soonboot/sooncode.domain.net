using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Generic;

[EventBoot(FuncType.delete, KeepAll = true)]
[Description("删除数据")]
public class BasicDeleteEvent : DomainEvent
{
}