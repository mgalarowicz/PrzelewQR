using PrzelewyQR;
using Soneta.Business;
using Soneta.Business.UI;
using Soneta.Core;
using Soneta.Kasa;
using Soneta.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

[assembly: FolderView("Ewidencja Środków Pieniężnych/Skanowanie przelewów",
    Priority = 100000,
    Description = "Skaner",
    ObjectType = typeof(PrzelewQR),
    ObjectPage = "PrzelewQR.ogolne.pageform.xml",
    ReadOnlySession = false,
    ConfigSession = false,
    IconName = "kod_kreskowy"
)]

namespace PrzelewyQR
{
    public class PrzelewQR : ContextBase, INewBarCode 
    {

        private readonly PodmiotKasowyLookupHelper _podmiotLookup;
        private RachunekBankowyFirmy _rachunek;
        private IPodmiotKasowy _podmiot;
        private Dictionary<string, string> _qr;
        private string[] _nazwyPozycji;


        public PrzelewQR(Context context) : base(context)
        {
            Rachunek = (RachunekBankowyFirmy)KasaModule.GetInstance(Context).EwidencjeSP.RachunekBankowy;
            _podmiotLookup = new PodmiotKasowyLookupHelper();
            _qr = new Dictionary<string, string>();
            _nazwyPozycji = new string[]
            {
                "Identyfikator Odbiorcy",
                "Kod Kraju",
                "Numer Rachunku Odbiorcy",
                "Kwota w Groszach",
                "Nazwa Odbiorcy",
                "Tytuł Płatności",
                "Rezerwa 1",
                "Rezerwa 2",
                "Rezerwa 3"
            };
        }

        public RachunekBankowyFirmy Rachunek 
        { 
           get => _rachunek;
           set
            {
                _rachunek = value;
                OnChanged(new EventArgs());
            }
        }

        public object GetListRachunek()
                    => KasaModule.GetInstance(Context).EwidencjeSP.GetModernLookup(EwidencjeSP.ModernViewArgs.Rachunki());


        public IPodmiotKasowy Podmiot
        {
            get => _podmiot;
            set
            {
                _podmiot = value;
                OnChanged(new EventArgs());
            }
        }

        public object GetListPodmiot()
            => _podmiotLookup.GetList(Context.Session, PodmiotKasowyLookupTyp.Kontrahenci);

        public object Enter(Context cx, string code, double quantity)
        {       
            string[] splitCode = code.Split('|');

            for (int i = 0; i < splitCode.Length; i++)
            {
                _qr[_nazwyPozycji[i]] = splitCode[i];
            }

            var numerRachunku = _qr["Kod Kraju"] + _qr["Numer Rachunku Odbiorcy"];

            var tytuł = _qr["Tytuł Płatności"];

            var kwotaDec = Convert.ToDecimal(_qr["Kwota w Groszach"].Insert(_qr["Kwota w Groszach"].Length - 2, ","));

            var kwota = new Currency(kwotaDec);

            var kasa = Context.Session.GetKasa();

            var rachunekBankowyPodmiotu = kasa.RachBankPodmiot.WgRachunekBank.Where(x => Regex.Replace(x.Rachunek.Numer.Pełny, @"\s+", "") == numerRachunku).FirstOrDefault();

            var podmiot = rachunekBankowyPodmiotu.Podmiot;

            using (ITransaction t = cx.Session.Logout(true))
            {
                var przelew = cx.Session.AddRow(new Przelew(Rachunek));

                przelew.Podmiot = podmiot;
                przelew.Rachunek = rachunekBankowyPodmiotu;
                przelew.Kwota = kwota;
                przelew.Data = Date.Now;
                przelew.Tytulem1 = tytuł;

                t.Commit();
            }

            cx.Session.InvokeChanged();

            return FormAction.None;

        }

        public ViewInfo PrzelewyViewInfo
        {
            get
            {
                var vi = new ViewInfo();

                vi.InitContext += (sender, args) =>
                {

                };

                vi.CreateView += (sender, args) =>
                {
                   var kasa = args.Context.Session.GetKasa();

                   var view = kasa.Przelewy.WgPodmiot.CreateView();

                   if (Rachunek != null)
                      view.Condition &= new FieldCondition.Equal("EwidencjaSP", Rachunek);
                            
                   if (Podmiot != null)
                      view.Condition &= new FieldCondition.Equal("Podmiot", Podmiot);

                   args.View = view;
                };

                return vi;
            }
        }
    }
}
