using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text;

namespace Smec
{
   
    public class OwnerTrackingCollection<T, P> : ObservableCollection<T> where T : IOwnable<P>
    {
        private readonly P _owner;

        
        public OwnerTrackingCollection(P owner)
        {
            // Добавили проверку на null, чтобы защитить код от непредвиденных ошибок
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }


        // Срабатывает при .Add() и .Insert()
        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            item?.Owner = _owner;
        }

        // Срабатывает при замене элемента: list[i] = newChild;
        protected override void SetItem(int index, T item)
        {
            // Защита от выхода за границы и null
            if (index >= 0 && index < Count)
            {
                this[index]?.Owner = default;
            }

            base.SetItem(index, item);
            item?.Owner = _owner;
        }

        // Срабатывает при .Remove() и .RemoveAt()
        protected override void RemoveItem(int index)
        {
            // Сначала безопасно сбрасываем владельца у удаляемого элемента
            if (index >= 0 && index < Count && this[index] != null)
            {
                this[index].Owner = default;
            }
            base.RemoveItem(index);
        }

        // Срабатывает при .Clear()
        protected override void ClearItems()
        {
            foreach (var item in this)
            {
                item?.Owner = default;
            }
            base.ClearItems();
        }
    }


}
