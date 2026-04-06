


public delegate void Callback();
public delegate void Callback<T>(T arg1);
public delegate void Callback<T, U>(T arg1, U arg2);
public delegate void Callback<T, U, V>(T arg1, U arg2, V arg3);
public delegate R CallbackReceiver<R>();
public delegate R CallbackReceiver<R, T>(T arg1);
public delegate R CallbackReceiver<R, T, U>(T arg1, U arg2);
public delegate R CallbackReceiver<R, T, U, V>(T arg1, U arg2, V arg3);
