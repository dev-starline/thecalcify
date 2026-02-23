using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Enum
{
    public enum HubMethodName
    {
        ReceiveAllClient,
        ReceiveMessage,
        UserListOfSymbol,
        UserConnected,
        UserDisconnected,
        excelRate,
        Error,
        ReceiveNewsNotification
        // add more as needed
    }

}
