using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Generic;

[EventBoot(FuncType.add, KeepAll = true)]
[Description("添加数据")]
public class BasicAddEvent : DomainEvent
{
}