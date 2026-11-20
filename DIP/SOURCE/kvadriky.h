///////////////////////////////////////////////////////////////////////////////////////////////
//
// KVADRIKY.H
//

#ifndef KVADRIKY_H
#define KVADRIKY_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGula
//
//		trieda zdedena z CPrvok
//		umoznuje pracu so svetlom v scene

class CGula : public CPrvok {
//DATA:
	private:						 

		// poloner:
		GLdouble polomer;

		// kvalita vykreslenia gule:
		GLint qualityX, qualityY;
// METODY:

		CGula () {}
	public:


		CGula(char *name) : CPrvok (name) {
			polomer = 2;				// nech je default taky ...
			qualityX = qualityY = 22;	// nech ma gula default 49 (=7x7) plosiek
			typPrvku = TYP_GULA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
		}

		// konstruktor vyuzivany na nacitanie gule zo suboru:
		CGula(ifstream& subor) : CPrvok(subor) {
			subor >> polomer;
			subor >> qualityX >> qualityY;
			typPrvku = TYP_GULA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}

   		CGula(CPrvok * source) : CPrvok(source) {
			polomer=((CGula*)source)->polomer;
			qualityX=((CGula*)source)->qualityX;
            qualityY=((CGula*)source)->qualityY;
			typPrvku = TYP_GULA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}



	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);

		// pre nacitanie sa zo suboru pocas 'zivota':
		void ReadYou(ifstream& subor);


		// ovladanie zmeny polomera gule, a kvality jej zobrazovania:
		GLdouble GetPolomer(GLvoid) {return polomer;}
		GLint   GetQualityX(GLvoid) {return qualityX;}
		GLint   GetQualityY(GLvoid) {return qualityY;}
		GLvoid  SetPolomer(GLdouble hodnota) {polomer = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetQualityX(GLint hodnota) { qualityX = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetQualityY(GLint hodnota) { qualityY = hodnota;}	// zatial bez obmedzeni !!!
        GLvoid  CGula::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);
		// zmena mierky (pomerna zmena velkosti) Prvku sceny - gule:
		GLvoid Resize(GLfloat hodnota) {
			if (hodnota > 0)
				polomer *= hodnota;
		}

		// toto je podstatna metoda = "zobrazenie" gule v priestore:
		GLvoid Render(GLenum style);
};


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CZrezkuzel
//
//		trieda zdedena z CPrvok
//		umoznuje pracu so zrezanym kuzelom v scene
//

class CZrezkuzel : public CPrvok {
// DATA
	private:

		// polomer1 specifies the radius of the cylinder at z = 0
		// polomer2 specifies the radius of the cylinder at z = dlzka
		// dlzka	specifies the height of the cylinder
		GLdouble polomer1,polomer2,dlzka;

		// kvalita vykreslenia zrezaneho kuzela:
		GLint qualityX,qualityY;

//METODY
		CZrezkuzel () {}

	public:

	CZrezkuzel (char *name) : CPrvok (name) {
		polomer1 = 1.0;
		polomer2 = 2.0;
		dlzka = 2.0;
		qualityX = qualityY = 22;
		typPrvku = TYP_ZREZANY_KUZEL;
	}

		// konstruktor vyuzivany na nacitanie  zo suboru:
		CZrezkuzel(ifstream& subor) : CPrvok(subor) {
			subor >> polomer1 >> polomer2 >> dlzka;
			subor >> qualityX >> qualityY;
			typPrvku = TYP_ZREZANY_KUZEL; //zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}

   		CZrezkuzel(CPrvok * source) : CPrvok(source) {
		polomer1=((CZrezkuzel*)source)->polomer1;
        polomer2=((CZrezkuzel*)source)->polomer2;
        dlzka=((CZrezkuzel*)source)->dlzka;
        qualityX=((CZrezkuzel*)source)->qualityX;
        qualityY=((CZrezkuzel*)source)->qualityY;
		typPrvku = TYP_ZREZANY_KUZEL; //zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}





	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);

		// pre nacitanie sa zo suboru pocas 'zivota':
		void ReadYou(ifstream& subor);

    GLvoid  VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);

	GLdouble GetPolomer1(GLvoid)  {return polomer1;}
	GLdouble GetPolomer2(GLvoid)  {return polomer2;}
	GLdouble GetDlzka(GLvoid)	  {return dlzka;}
	GLint    GetQualityX(GLvoid)  {return qualityX;}
	GLint    GetQualityY(GLvoid)  {return qualityY;}
	GLvoid   SetPolomer1(GLdouble hodnota) {polomer1 = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid   SetPolomer2(GLdouble hodnota) {polomer2 = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid   SetDlzka(GLdouble hodnota)	   {dlzka    = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid   SetQualityX(GLint hodnota)	   {qualityX = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid   SetQualityY(GLint hodnota)    {qualityY = hodnota;}	// zatial bez obmedzeni !!!

	// zmena mierky (pomerna zmena velkosti) Prvku sceny - zrezaneho kuzela:
	GLvoid   Resize (GLfloat hodnota){
			if (hodnota > 0) {
				polomer1 *= hodnota;
				polomer2 *= hodnota;
				dlzka    *= hodnota;
			}
	}

	// toto je podstatna metoda = "zobrazenie"  zrezaneho kuzela v priestore:
	GLvoid   Render(GLenum style);

};	// CZrezkuzel

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CDisk
//
//		trieda zdedena z CPrvok
//		umoznuje pracu s plochou tvaru disku v scene, ktoremu sa daju menit:
//			polomer1 (vonkajsi)
//			polomer2 (vnutorny)	- ak je > 0 , tak tento Prvok tvori plochu prstenca
//			uhol1	 - ak tento uhol je 360 disk (prstenec) je uzavrety, inak je to urcity
//					   vysek disku (prstenca)
//

class CDisk : public CPrvok {
// DATA
	private:

		// polomer1 (vonkajsi)
		// polomer2 (vnutorny)
		// uhol1 specifies the sweep angle, in degrees, of the disk portion
		GLdouble polomer1,polomer2,uhol1;

		// kvalita vykreslenia zrezaneho kuzela:
		GLint qualityX,qualityY;

	//METODY
	CDisk () {}
	public:


		CDisk (char *name) : CPrvok (name) {
			polomer1 = 1.0;
			polomer2 = 2.0;
			uhol1=270.0;
			qualityX = qualityY = 22;
			typPrvku = TYP_DISK;
		}

		// konstruktor vyuzivany na nacitanie  zo suboru:
		CDisk(ifstream& subor) : CPrvok(subor) {
			subor >> polomer1 >> polomer2 >> uhol1;
			subor >> qualityX >> qualityY;
			typPrvku = TYP_DISK; //zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}


   		CDisk(CPrvok* source) : CPrvok(source) {
		polomer1=((CDisk*)source)->polomer1;
        polomer2=((CDisk*)source)->polomer2;
        uhol1=((CDisk*)source)->uhol1;
		qualityX=((CDisk*)source)->qualityX;
        qualityY=((CDisk*)source)->qualityY;
		typPrvku = TYP_DISK; //zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}



	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);

		// pre nacitanie sa zo suboru pocas 'zivota':
		void ReadYou(ifstream& subor);

    GLvoid  VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);

	GLdouble GetPolomer1(GLvoid)  {return polomer1;}
	GLdouble GetPolomer2(GLvoid)  {return polomer2;}
	GLdouble GetUhol1(GLvoid)  {return uhol1;}
	GLint   GetQualityX(GLvoid) {return qualityX;}
	GLint   GetQualityY(GLvoid) {return qualityY;}
	GLvoid  SetPolomer1(GLdouble hodnota) {polomer1 = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid  SetPolomer2(GLdouble hodnota) {polomer2 = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid  SetQualityX(GLint hodnota) { qualityX = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid  SetQualityY(GLint hodnota) { qualityY = hodnota;}	// zatial bez obmedzeni !!!
	GLvoid  SetUhol1(GLdouble hodnota) {uhol1 = hodnota;}	// zatial bez obmedzeni !!!

	// zmena mierky (pomerna zmena velkosti) Prvku sceny - disku:
	GLvoid   Resize (GLfloat hodnota){
			if (hodnota > 0) {
				polomer1 *= hodnota;
				polomer2 *= hodnota;
			}
	}

	// toto je podstatna metoda = "zobrazenie" disku v priestore:
	GLvoid   Render(GLenum style);

};	// koniec CDisk


#endif  // KVADRIKY_H
