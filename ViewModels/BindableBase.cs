using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Furtherance.ViewModels;
public class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    protected bool SetProperty<T>(ref T originalValue, T newValue, [CallerMemberName] string propertyName = null)
    {
        if (Equals(originalValue, newValue))
        {
            return false;
        }
        originalValue = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }
}
