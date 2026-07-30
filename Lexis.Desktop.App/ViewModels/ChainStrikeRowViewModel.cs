using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lexis.Contracts.Market;
using Lexis.Desktop.App.ViewModels.Documents;

namespace Lexis.Desktop.App.ViewModels;

public partial class ChainStrikeRowViewModel : ObservableObject
{
    public OptionChainDocumentViewModel? Owner { get; set; }

    [ObservableProperty] private double _strike;
    [ObservableProperty] private double _callBid;
    [ObservableProperty] private double _callAsk;
    [ObservableProperty] private double _callIv;
    [ObservableProperty] private double _callDelta;
    [ObservableProperty] private double _callGamma;
    [ObservableProperty] private double _callTheta;
    [ObservableProperty] private double _callVega;
    [ObservableProperty] private int _callVol;
    [ObservableProperty] private int _callOi;

    [ObservableProperty] private double _putBid;
    [ObservableProperty] private double _putAsk;
    [ObservableProperty] private double _putIv;
    [ObservableProperty] private double _putDelta;
    [ObservableProperty] private double _putGamma;
    [ObservableProperty] private double _putTheta;
    [ObservableProperty] private double _putVega;
    [ObservableProperty] private int _putVol;
    [ObservableProperty] private int _putOi;

    [ObservableProperty] private bool _isAtm;
    [ObservableProperty] private bool _isFlowFocus;

    public IBrush RowBackground =>
        IsFlowFocus ? SolidColorBrush.Parse("#3B2F1A")
        : IsAtm ? SolidColorBrush.Parse("#1A2F4A")
        : SolidColorBrush.Parse("#0B1220");

    public void Apply(ChainRowDto row, double spot, double strikeStep)
    {
        Strike = row.Strike;
        IsAtm = Math.Abs(row.Strike - spot) <= strikeStep * 0.51;

        CallBid = row.Call.Bid;
        CallAsk = row.Call.Ask;
        CallIv = row.Call.Iv;
        CallDelta = row.Call.Delta;
        CallGamma = row.Call.Gamma;
        CallTheta = row.Call.Theta;
        CallVega = row.Call.Vega;
        CallVol = row.Call.Vol;
        CallOi = row.Call.Oi;

        PutBid = row.Put.Bid;
        PutAsk = row.Put.Ask;
        PutIv = row.Put.Iv;
        PutDelta = row.Put.Delta;
        PutGamma = row.Put.Gamma;
        PutTheta = row.Put.Theta;
        PutVega = row.Put.Vega;
        PutVol = row.Put.Vol;
        PutOi = row.Put.Oi;
    }

    // TOS convention: Ask → BUY (green), Bid → SELL (red)
    [RelayCommand] private void ClickCallAsk() => Owner?.PlaceStub("CALL", "BUY", Strike, CallAsk);
    [RelayCommand] private void ClickCallBid() => Owner?.PlaceStub("CALL", "SELL", Strike, CallBid);
    [RelayCommand] private void ClickPutAsk() => Owner?.PlaceStub("PUT", "BUY", Strike, PutAsk);
    [RelayCommand] private void ClickPutBid() => Owner?.PlaceStub("PUT", "SELL", Strike, PutBid);
}
