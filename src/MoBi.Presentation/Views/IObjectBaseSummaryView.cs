﻿using MoBi.Presentation.DTO;
using MoBi.Presentation.Presenter;
using OSPSuite.Presentation.Views;

namespace MoBi.Presentation.Views
{
   public interface IObjectBaseSummaryView : IView<IObjectBaseSummaryPresenter>
   {
      void BindTo(ObjectBaseSummaryDTO objectBaseDTO);
   }
}
