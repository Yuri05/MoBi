﻿using MoBi.Presentation.Presenter;
using OSPSuite.Presentation.Views;

namespace MoBi.Presentation.Views
{
   public interface IApplyToSelectionView : IView<IApplyToSelectionPresenter>
   {
      void BindToSelection();
   }
}