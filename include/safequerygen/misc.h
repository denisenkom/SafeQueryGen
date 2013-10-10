#ifndef SafeQueryGenH
#define SafeQueryGenH


#include <vector>
#include <windows.h>
#include <sysutils.hpp>
#include <adodb.hpp>


namespace SafeQueryGen
{
	void ThrowOleException(HRESULT hres);

	inline HRESULT OleCheckException(HRESULT hres)
	{
		if (FAILED(hres))
		{
			ThrowOleException(hres);
		}
		return hres;
	}

	extern const int LockTypeValues[];
	extern const int CursorLocationValues[];
	extern const int CursorTypeValues[];

	void OpenDatasetWithCommand(Adodb::TCustomADODataSet * dataset, Adodb::TADOCommand * command);

	class JetBugWrapper
	{
	public:
		JetBugWrapper()
		{
			DWORD cch = ::GetLocaleInfo(::GetUserDefaultLCID(), LOCALE_SSHORTDATE, NULL, 0);
			Win32Check(cch);
			_prevShortDate.resize(cch);
			Win32Check(::GetLocaleInfo(::GetUserDefaultLCID(), LOCALE_SSHORTDATE, &_prevShortDate[0], cch));
			Win32Check(::SetLocaleInfo(::GetUserDefaultLCID(), LOCALE_SSHORTDATE, _T("MM/dd/yyyy")));
		}

		~JetBugWrapper()
		{
			Win32Check(::SetLocaleInfo(::GetUserDefaultLCID(), LOCALE_SSHORTDATE, &_prevShortDate[0]));
		}
	private:
		std::vector<TCHAR> _prevShortDate;
	};

	Adodb::TADOConnection * GetDefaultConnection();
	void SetDefaultConnection(Adodb::TADOConnection * connection);

	void _RegisterLookup(Adodb::TCustomADODataSet ** ptrptr);
	void FreeAllLookups();

#define JET_BUG_WRAPPING_BEGIN { SafeQueryGen::JetBugWrapper __bugWrapper;
#define JET_BUG_WRAPPING_END }

	inline Adodb::TADOLockType _LockTypeShutWarn(signed char lockType) { return static_cast<Adodb::TADOLockType>(lockType); }
	inline Adodb::TCursorType _CursorTypeShutWarn(signed char cursorType) { return static_cast<Adodb::TCursorType>(cursorType); }

} // namespace SafeQueryGen


#endif // SafeQueryGenH
