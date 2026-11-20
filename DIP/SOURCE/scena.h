///////////////////////////////////////////////////////////////////////////////////////////////
//
// SCENA.H
//

#ifndef SCENA_H
#define SCENA_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CSCENA
//
//		trieda zdedena z CPrvok
//		umoznuje pracu so svetlom v scene

class CScena : public CGrupa {
//DATA:
	private:
	GLfloat aniTime; //cas sceny v animacii
	GLint typHmly; //urcuje typ hmly -> linear, exp, a aj to ci je hmla zapnuta/vypnuta
	GLfloat zaciatokHmly, koniecHmly, hustotaHmly;
	GLfloat rHmla, gHmla, bHmla, aHmla; // rgba parametre farby hmly

//    BOOL svetlo[8];
    CSvetlo *prsvetlo[8];
    CKamera *prkamera[8];


// METODY:

		// zakazane je vytvarat akykolvek Prvok bez mena:
		CScena() : CGrupa ("No_Name_Prvok_Sceny") {}

	public:
//       CLightSupervisor lightSupervisor;
//		CPrvok* predosly;

		// defaulty - prazdna scena:
        CKamera *bocna, *predna, *horna;

CScena(char *name) : CGrupa(name) {

    bocna = new CKamera("BocnaKamera");
    predna = new CKamera("PrednaKamera");
    horna = new CKamera("HornaKamera");

    predna->SetX(0.0);
    predna->SetY(0.0);
    predna->SetZ(40.0);
    predna->SetAlfa(0.0);
    predna->SetBeta(0.0);
    predna->SetGama(0.0);

    bocna->SetX(40.0);
    bocna->SetY(0.0);
    bocna->SetZ(0.0);
    bocna->SetAlfa(0.0);
    bocna->SetBeta(90.0);
    bocna->SetGama(0.0);

    horna->SetX(0.0);
    horna->SetY(40.0);
    horna->SetZ(0.0);
    horna->SetAlfa(-90.0);
    horna->SetBeta(0.0);
    horna->SetGama(0.0);

    int i;
    for(i=0; i<8; i++) {
        prsvetlo[i] = NULL;
        prkamera[i] = NULL;
        }

	aniTime = 0;
	typHmly = NONE;
	zaciatokHmly = 0.0;
	koniecHmly = 1.0;
	hustotaHmly = 1.0;
	rHmla = gHmla = bHmla = 0.2;
	aHmla = 0.2;
    typPrvku = TYP_SCENA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
}
  /*
		// konstruktor vyuzivany na nacitanie sceny zo suboru:
		CScena(ifstream& subor) : CGrupa(subor, FALSE)  {
			subor >> aniTime;
			subor >> typHmly;
			subor >> zaciatokHmly;
			subor >> koniecHmly;
			subor >> hustotaHmly;
			subor >> rHmla >> gHmla >> bHmla;
			subor >> aHmla ;

			typPrvku = TYP_SCENA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}
    */



	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);
        int LoadYou(ifstream& subor);
        int LoadYou(CPrvok *source);

		// pre nacitanie sa zo suboru pocas 'zivota' pre grupu nema zmysel (iba pre zobrazitelny prvok):
		// (odvirtualnenie CPrvku)
		//void ReadYou(ifstream& subor) {}
 		~CScena() {  /*
       		CPrvok *temp = prvy;

			// treba zmazat celu retaz:
			while (prvy) {
				prvy = prvy->dalsi;
				delete temp;
				temp = prvy;
			}   */
      }
		// toto je podstatna metoda = "zobrazenie" sceny v priestore:
		GLvoid Render(GLenum style);

//		CPrvok* NajdiPrvok (char * name);
//		CPrvok* ZmazPrvok  (CPrvok *prvok);
//		GLvoid Ungroup     (int ID);

		GLfloat GetAniTime (GLvoid) { return aniTime;}
		GLvoid  SetAniTime (GLfloat hodnota) {aniTime = hodnota;}
		GLfloat GetZaciatokHmly (GLvoid) { return zaciatokHmly;}
		GLvoid  SetZaciatokHmly (GLfloat hodnota) {zaciatokHmly = hodnota;}
		GLfloat GetKoniecHmly (GLvoid) { return koniecHmly;}
		GLvoid  SetKoniecHmly (GLfloat hodnota) {koniecHmly = hodnota;}
		GLfloat GetHustotaHmly (GLvoid) { return hustotaHmly;}
		GLvoid  SetHustotaHmly (GLfloat hodnota) {hustotaHmly = hodnota;}
		GLfloat GetRHmla (GLvoid) { return rHmla;}
		GLvoid  SetRHmla (GLfloat hodnota) {rHmla = hodnota;}
		GLfloat GetGHmla (GLvoid) { return gHmla;}
		GLvoid  SetGHmla (GLfloat hodnota) {gHmla = hodnota;}
		GLfloat GetBHmla (GLvoid) { return bHmla;}
		GLvoid  SetBHmla (GLfloat hodnota) {bHmla = hodnota;}
		GLfloat GetAHmla (GLvoid) { return aHmla;}
		GLvoid  SetAHmla (GLfloat hodnota) {aHmla = hodnota;}
		GLint   GetTypHmly (GLvoid) { return typHmly;}
		GLvoid  SetTypHmly (GLint hodnota) {typHmly = hodnota;}




		CSvetlo* VytvorNoveSvetlo(char *);
		// na vytvorenie svetla nacitanim jeho vlastnosti zo suboru !
		CSvetlo* VytvorNoveSvetlo(ifstream& subor);
		CSvetlo* VytvorNoveSvetlo(CPrvok* source);
        void CScena::ZrusKameru(CPrvok *temp);
        CKamera* CScena::VytvorNovuKameru(char *name);
        CKamera* CScena::VytvorNovuKameru(CPrvok *source);
        CKamera* CScena::VytvorNovuKameru(ifstream& subor);
        void CScena::SetAktualna(CPrvok* temp);
        void VypocitajMedziscenu( float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CScena* predPrvaScena,CScena* druhaScena,CScena* zaDruhaScena);
		void ZrusSvetlo(CSvetlo * svetlo);


};	// koniec CSCENA


#endif  // SCENA_H
